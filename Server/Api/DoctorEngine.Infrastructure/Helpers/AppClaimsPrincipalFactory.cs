using DoctorEngineApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace DoctorEngineApp.Helpers
{
    /// <summary>
    /// Profil şəklini və ad soyadı login cookie-sinə claim kimi yazır,
    /// beləliklə header hər sorğuda bazaya müraciət etmədən avatarı göstərir.
    /// Dəyişiklikdən sonra SignInManager.RefreshSignInAsync çağırılmalıdır.
    /// </summary>
    public class AppClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
    {
        public const string AvatarClaim = "avatar";
        public const string FullNameClaim = "full_name";

        public AppClaimsPrincipalFactory(UserManager<AppUser> userManager,
                                         RoleManager<IdentityRole> roleManager,
                                         IOptions<IdentityOptions> options)
            : base(userManager, roleManager, options) { }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            if (!string.IsNullOrEmpty(user.ProfileImage))
                identity.AddClaim(new Claim(AvatarClaim, user.ProfileImage));

            if (!string.IsNullOrEmpty(user.FullName))
                identity.AddClaim(new Claim(FullNameClaim, user.FullName));

            return identity;
        }
    }
}
