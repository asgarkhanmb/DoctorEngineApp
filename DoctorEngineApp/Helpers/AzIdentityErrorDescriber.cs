using Microsoft.AspNetCore.Identity;

namespace DoctorEngineApp.Helpers
{
    /// <summary>ASP.NET Identity xəta mesajlarının Azərbaycan dilində tərcüməsi.</summary>
    public class AzIdentityErrorDescriber : IdentityErrorDescriber
    {
        private static IdentityError E(string code, string text) => new() { Code = code, Description = text };

        public override IdentityError DefaultError() => E(nameof(DefaultError), "Naməlum xəta baş verdi.");
        public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Məlumat başqa yerdən dəyişdirilib. Yenidən cəhd edin.");
        public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "Şifrə yanlışdır.");
        public override IdentityError InvalidToken() => E(nameof(InvalidToken), "Link etibarsızdır və ya vaxtı bitib. Yenisini tələb edin.");
        public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Bu giriş artıq başqa hesaba bağlıdır.");
        public override IdentityError InvalidUserName(string userName) => E(nameof(InvalidUserName), $"İstifadəçi adı \"{userName}\" yanlışdır. Yalnız hərf və rəqəm istifadə edin.");
        public override IdentityError InvalidEmail(string email) => E(nameof(InvalidEmail), $"Email \"{email}\" düzgün deyil.");
        public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), $"\"{userName}\" istifadəçi adı artıq tutulub.");
        public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), $"\"{email}\" ünvanı ilə artıq qeydiyyat var.");
        public override IdentityError InvalidRoleName(string role) => E(nameof(InvalidRoleName), $"Rol adı \"{role}\" yanlışdır.");
        public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), $"\"{role}\" rolu artıq mövcuddur.");
        public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "İstifadəçinin artıq şifrəsi var.");
        public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "Bu istifadəçi üçün bloklama aktiv deyil.");
        public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), $"İstifadəçi artıq \"{role}\" rolundadır.");
        public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), $"İstifadəçi \"{role}\" rolunda deyil.");
        public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"Şifrə minimum {length} simvol olmalıdır.");
        public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"Şifrədə ən azı {uniqueChars} fərqli simvol olmalıdır.");
        public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "Şifrədə ən azı bir xüsusi simvol olmalıdır (məs: ! @ # $).");
        public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "Şifrədə ən azı bir rəqəm olmalıdır (0–9).");
        public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "Şifrədə ən azı bir kiçik hərf olmalıdır (a–z).");
        public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "Şifrədə ən azı bir böyük hərf olmalıdır (A–Z).");
    }
}
