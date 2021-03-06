﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssemblyToProcess.Attributes.Contracts;
using NUnit.Framework;

namespace Mimick.Tests.Attributes.Contracts
{
    [TestFixture]
    public class NotNullTest
    {
        private NotNullAttributes target;

        [OneTimeSetUp]
        public void Initialize() => target = new NotNullAttributes();

        [Test]
        public void ShouldPassWhenNotNullIsNotPresent() => target.PassIfNull(null);

        [Test]
        public void ShouldThrowWhenNotNullArgumentIsPassedNull() => Assert.Throws(typeof(ArgumentNullException), () => target.ThrowIfNull(null));

        [Test]
        public void ShouldPassWhenNotNullArgumentIsPassedValid() => target.ThrowIfNull(new object());

        [Test]
        public void ShouldThrowWhenNotNullMethodIsPassedNull() => Assert.Throws(typeof(ArgumentNullException), () => target.ThrowIfAnyNull(new object(), null));

        [Test]
        public void ShouldPassWhenNotNullMethodIsPassedValid() => target.ThrowIfAnyNull(new object(), new object());

        [Test]
        public void ShouldThrowWhenNotNullReturnIsReturningNull() => Assert.Throws(typeof(ArgumentNullException), () => target.ThrowIfReturnsNull(null));

        [Test]
        public void ShouldPassWhenNotNullReturnIsReturningValid() => target.ThrowIfReturnsNull(new object());
    }
}
