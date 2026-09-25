using System;

namespace negosuite_api.Models
{
    public partial class UserLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public DateTime SignInDate { get; set; }
        public string AccessToken { get; set; }
        public string Platform { get; set; }
        public string PlatformVersion { get; set; }
        public string RefreshToken { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public bool? IsRevoked { get; set; }
    }
}
