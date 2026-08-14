using AgriDrone.Modules.Identity.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using BCryptNet = BCrypt.Net.BCrypt;
namespace AgriDrone.Modules.Identity.Infrastructure.Authentication
{
    internal sealed class PasswordService : IPasswordService
    {
        public bool VerifyPassword(string password, string hashedPassword)
        {
            return BCryptNet.Verify(password, hashedPassword);
        }
        public string HashPassword(string password)
        {
            return BCryptNet.HashPassword(password);
        }
    }
}
