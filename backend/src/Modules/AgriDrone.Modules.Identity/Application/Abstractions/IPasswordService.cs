using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Abstractions
{
    internal interface IPasswordService
    {
        bool VerifyPassword(string password, string hashedPassword);
        string HashPassword(string password);
    }
}
