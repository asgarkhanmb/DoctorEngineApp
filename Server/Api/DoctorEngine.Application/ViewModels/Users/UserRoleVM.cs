namespace DoctorEngineApp.ViewModels.Users
{
    public class UserRoleVM
    {
        public string Id { get; set; } = "";
        public string Fullname { get; set; } = "";
        public string Username { get; set; } = "";
        public string Email { get; set; } = "";
        public bool EmailConfirmed { get; set; }
        public bool IsLockedOut { get; set; }
        public List<string> RoleList { get; set; } = new();

        // Köhnə görünüşlər üçün
        public string Roles => string.Join(", ", RoleList);
    }
}
