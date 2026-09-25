using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace negosuite_api.Models
{
    public partial class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string MobileNo { get; set; }
        public string Password { get; set; }
        public string Name { get; set; }
        public int? UserTypeId { get; set; }
        public int? UserRoleId { get; set; }
        public string Avatar { get; set; }
        public bool Status { get; set; }
        public int? ConfigId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual UserRole UserRole { get; set; }
        public virtual UserType UserType { get; set; }
        public virtual Config Config { get; set; }
        public string UserUIConfig { get; set; }

        [NotMapped]
        public virtual string Identifier { get; set; }
    }
}
