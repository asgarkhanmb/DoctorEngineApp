namespace DoctorEngineApp.Helpers
{
    public static class EmailTemplates
    {
        public static string ConfirmEmail(string url)
        {
            return $@"
            <h2>Email təsdiqi</h2>
            <p>Hesabınızı təsdiqləmək üçün aşağıdakı düyməyə klik edin</p>
            <a href='{url}' style='
                padding:10px 20px;
                background:#0d6efd;
                color:white;
                text-decoration:none;
                border-radius:5px'>
                Hesabı təsdiqlə
            </a>";
        }

        public static string ResetPassword(string url)
        {
            return $@"
            <h2>Şifrəni yenilə</h2>
            <p>Şifrəni dəyişmək üçün aşağıdakı düyməyə klik edin</p>
            <a href='{url}' style='
                padding:10px 20px;
                background:red;
                color:white;
                text-decoration:none;
                border-radius:5px'>
                Şifrəni dəyiş
            </a>";
        }
    }
}