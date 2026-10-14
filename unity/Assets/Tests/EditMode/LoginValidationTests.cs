using NUnit.Framework;
using ProcSense.VR.Login;

namespace ProcSense.Tests.EditMode
{
    public class LoginValidationTests
    {
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not-an-email")]
        [TestCase("person@@example.com")]
        [TestCase("person@example")]
        [TestCase("person@example..com")]
        [TestCase("person@-example.com")]
        [TestCase("person..name@example.com")]
        [TestCase("Name <person@example.com>")]
        [TestCase("person\0@example.com")]
        public void Malformed_email_is_rejected(string value) => Assert.IsNotEmpty(LoginInputValidation.EmailError(value));

        [TestCase("demo@procsense.app")]
        [TestCase("  DEMO@PROCSENSE.APP  ")]
        [TestCase("person+training@example.co.uk")]
        public void Common_email_and_boundary_whitespace_are_accepted(string value) => Assert.IsEmpty(LoginInputValidation.EmailError(value));

        [Test]
        public void Length_limits_and_missing_values_are_rejected()
        {
            Assert.IsNotEmpty(LoginInputValidation.EmailError(null));
            Assert.IsNotEmpty(LoginInputValidation.EmailError(new string('a', 65) + "@example.com"));
            Assert.IsNotEmpty(LoginInputValidation.EmailError(new string('a', 255)));
            Assert.IsNotEmpty(LoginInputValidation.PasswordError(null));
            Assert.IsNotEmpty(LoginInputValidation.PasswordError("   "));
            Assert.IsNotEmpty(LoginInputValidation.PasswordError("hello\nworld"));
            Assert.IsNotEmpty(LoginInputValidation.PasswordError(new string('a', 129)));
            Assert.IsEmpty(LoginInputValidation.PasswordError(new string('a', 128)));
            Assert.IsEmpty(LoginInputValidation.PasswordError(" password with spaces "));
        }
    }
}
