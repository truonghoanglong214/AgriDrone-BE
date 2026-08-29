namespace AgriDrone.Modules.Identity.Application.Abstractions.Services
{
    internal interface IPasswordService
    {
        bool VerifyPassword(string password, string hashedPassword);
        string HashPassword(string password);
    }
}
