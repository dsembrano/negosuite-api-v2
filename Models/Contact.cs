using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class Contact
    {
        public Contact()
        {
            ContactPhones = new HashSet<ContactPhone>();
        }

        public int Id { get; set; }
        public string FirstName { get; set; }
        public string Lastname { get; set; }
        public string Title { get; set; }
        public string Email { get; set; }
        public string AlternateEmail { get; set; }
        public int? PhotoId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual ICollection<ContactPhone> ContactPhones { get; set; }
    }
}
