using System;

namespace negosuite_api.Models
{
    public partial class EmailLog
    {
        public int Id { get; set; }
        public string Uuid { get; set; }
        public string Email { get; set; }
        public string Data { get; set; }
        public int? ConfigId { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public int Status { get; set; }
        public string Action { get; set; }

    }
}
