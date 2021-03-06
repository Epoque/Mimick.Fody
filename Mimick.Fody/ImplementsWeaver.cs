﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mimick.Fody.Weavers;
using Mono.Cecil;
using Mono.Cecil.Rocks;

/// <summary>
/// A class containing methods for weaving an implementation into a class.
/// </summary>
public partial class ModuleWeaver
{
    /// <summary>
    /// Weaves all implementations.
    /// </summary>
    public void WeaveImplementations()
    {
        var candidates = Context.Candidates.FindTypeByImplements();

        foreach (var item in candidates)
        {
            var emitter = new TypeEmitter(ModuleDefinition, item.Type, Context);

            foreach (var attribute in item.Implements)
                WeaveImplementation(emitter, attribute);
        }
    }

    /// <summary>
    /// Weaves an implementation of a provided attribute type against a provided type.
    /// </summary>
    /// <param name="emitter">The emitter.</param>
    /// <param name="attribute">The attribute.</param>
    public void WeaveImplementation(TypeEmitter emitter, CustomAttribute attribute)
    {
        var implementType = attribute.AttributeType.Resolve();
        var interfaceType = attribute.GetAttribute(Context.Finder.CompilationImplementsAttribute)?.GetProperty<TypeReference>("Interface")?.Resolve();
                
        if (interfaceType == null)
            return;

        if (emitter.Target.Interfaces.Any(i => i.InterfaceType.FullName == interfaceType.FullName))
            return;

        if (interfaceType.HasGenericParameters)
            throw new NotSupportedException($"Cannot implement interface {interfaceType.FullName} due to generic parameters");

        var field = CreateAttribute(emitter, attribute);

        WeaveImplementation(emitter, implementType, interfaceType, field);
    }

    public void WeaveImplementation(TypeEmitter emitter, TypeDefinition implementType, TypeDefinition interfaceType, Variable field)
    {
        if (!implementType.Interfaces.Any(i => i.InterfaceType.FullName == interfaceType.FullName))
            throw new NotSupportedException($"Cannot implement attribute '{implementType.FullName}' as it does not implement '{interfaceType.FullName}'");

        if (emitter.Target.Interfaces.Any(i => i.InterfaceType.FullName == interfaceType.FullName))
            return;

        emitter.Target.Interfaces.Add(new InterfaceImplementation(interfaceType.Import()));

        foreach (var method in interfaceType.Methods.Concat(interfaceType.Interfaces.Select(i => i.InterfaceType.Resolve()).SelectMany(i => i.Methods)))
            WeaveImplementedMethod(emitter, field, method, interfaceType);

        foreach (var property in interfaceType.Properties)
            WeaveImplementedProperty(emitter, field, property, interfaceType);

        foreach (var evt in interfaceType.Events)
            WeaveImplementedEvent(emitter, field, evt, interfaceType);
    }

    /// <summary>
    /// Weave an implementation of an event against the provided type.
    /// </summary>
    /// <param name="emitter">The emitter.</param>
    /// <param name="field">The attribute field.</param>
    /// <param name="evt">The event.</param>
    /// <param name="interfaceType">The type of the interface.</param>
    public void WeaveImplementedEvent(TypeEmitter emitter, Variable field, EventReference evt, TypeDefinition interfaceType)
    {
        var implemented = field.Type.GetEvent(evt.Name, evt.EventType);

        if (implemented == null)
        {
            implemented = field.Type.GetEvent($"{interfaceType.FullName}.{evt.Name}", evt.EventType);

            if (implemented == null)
                throw new MissingMemberException($"Cannot implement '{field.Type.FullName}' as it does not implement event '{evt.Name}'");
        }

        var emitted = emitter.EmitEvent(evt.Name, implemented.EventType);
        var definition = evt.Resolve() ?? implemented.Resolve();

        var hasAdd = emitted.HasAdd;
        var add = emitted.GetAdd();

        if (!hasAdd)
        {
            add.Body.SimplifyMacros();

            var ail = add.GetIL();
            var interfaceAdd = definition.AddMethod.Import();

            ail.Emit(Codes.Nop);
            ail.Emit(Codes.ThisIf(field));
            ail.Emit(Codes.Load(field));
            ail.Emit(Codes.Arg(add.IsStatic ? 0 : 1));
            ail.Emit(Codes.Invoke(interfaceAdd.GetGeneric()));
            ail.Emit(Codes.Return);

            add.Body.OptimizeMacros();
        }


        add.Body.InitLocals = true;

        var hasRemove = emitted.HasRemove;
        var remove = emitted.GetRemove();

        if (!emitted.HasRemove)
        {
            remove.Body.SimplifyMacros();

            var ril = remove.GetIL();
            var interfaceRemove = definition.RemoveMethod.Import();

            ril.Emit(Codes.Nop);
            ril.Emit(Codes.ThisIf(field));
            ril.Emit(Codes.Load(field));
            ril.Emit(Codes.Arg(remove.IsStatic ? 0 : 1));
            ril.Emit(Codes.Invoke(interfaceRemove.GetGeneric()));
            ril.Emit(Codes.Return);

            remove.Body.OptimizeMacros();
        }

        remove.Body.InitLocals = true;
    }

    /// <summary>
    /// Weave an implementation of a method against the provided type.
    /// </summary>
    /// <param name="emitter">The emitter.</param>
    /// <param name="field">The attribute field.</param>
    /// <param name="method">The method.</param>
    /// <param name="interfaceType">The type of the interface.</param>
    public void WeaveImplementedMethod(TypeEmitter emitter, Variable field, MethodReference method, TypeDefinition interfaceType)
    {
        var resolved = method.Resolve();

        var emitted = emitter.EmitMethod(
            method.Name,
            method.ReturnType.Import(),
            parameterTypes: method.HasParameters ? method.Parameters.Select(p => p.ParameterType.Import()).ToArray() : new TypeReference[0],
            genericTypes: method.HasGenericParameters ? method.GenericParameters.ToArray() : new GenericParameter[0],
            toStatic: resolved.IsStatic,
            toVisibility: resolved.GetVisiblity()
        );

        var parameters = method.Parameters.Select(p => p.ParameterType).ToArray();
        var generics = method.GenericParameters.ToArray();
        var implemented = field.Type.GetMethod(method.Name, returns: method.ReturnType, parameters: parameters, generics: generics);

        if (implemented == null)
        {
            implemented = field.Type.GetMethod($"{interfaceType.FullName}.{method.Name}", returns: method.ReturnType, parameters: parameters, generics: generics);

            if (implemented == null)
                throw new MissingMemberException($"Cannot implement '{field.Type.FullName}' as it does not implement method '{method.FullName}'");
        }

        var il = emitted.GetIL();

        il.Emit(Codes.Nop);
        il.Emit(Codes.ThisIf(field));
        il.Emit(Codes.Load(field));

        for (int i = 0, count = method.Parameters.Count; i < count; i++)
            il.Emit(Codes.Arg(i + 1));

        il.Emit(Codes.Invoke(method.Import().GetGeneric()));
        il.Emit(Codes.Return);

        emitted.Body.InitLocals = true;
    }

    /// <summary>
    /// Weaves an implementation of a property against the provided type.
    /// </summary>
    /// <param name="emitter">The emitter.</param>
    /// <param name="field">The attribute field.</param>
    /// <param name="property">The property.</param>
    /// <param name="interfaceType">The type of the interface.</param>
    public void WeaveImplementedProperty(TypeEmitter emitter, Variable field, PropertyDefinition property, TypeDefinition interfaceType)
    {
        var emitted = emitter.EmitProperty(property.Name, property.PropertyType.Import(), toBackingField: true);
        var implemented = field.Type.GetProperty(property.Name, property.PropertyType)?.Resolve();

        if (implemented == null)
        {
            implemented = field.Type.GetProperty($"{interfaceType.FullName}.{property.Name}", returnType: property.PropertyType)?.Resolve();

            if (implemented == null)
                throw new MissingMemberException($"Cannot implement '{field.Type.FullName}' as it does not implement property '{property.Name}'");
        }

        var source = new PropertyEmitter(emitter, implemented);
        
        if (source.HasGetter && !emitted.HasGetter)
        {
            var getter = emitted.GetGetter();
            var il = getter.GetIL();
            var propertyGet = property.GetMethod?.Import() ?? implemented.GetMethod.Import();

            getter.Body.SimplifyMacros();

            il.Emit(Codes.Nop);
            il.Emit(Codes.ThisIf(field));
            il.Emit(Codes.Cast(interfaceType));
            il.Emit(Codes.Load(field));
            il.Emit(Codes.Invoke(propertyGet.GetGeneric()));
            il.Emit(Codes.Return);

            getter.Body.OptimizeMacros();
            getter.Body.InitLocals = true;
        }

        if (source.HasSetter && !emitted.HasSetter)
        {
            var setter = emitted.GetSetter();
            var il = setter.GetIL();
            var propertySet = property.SetMethod?.Import() ?? implemented.SetMethod.Import();

            setter.Body.SimplifyMacros();

            il.Emit(Codes.Nop);
            il.Emit(Codes.ThisIf(field));
            il.Emit(Codes.Load(field));
            il.Emit(Codes.Arg(setter.Target.IsStatic ? 0 : 1));
            il.Emit(Codes.Invoke(propertySet.GetGeneric()));
            il.Emit(Codes.Return);

            setter.Body.OptimizeMacros();
            setter.Body.InitLocals = true;
        }
    }
}