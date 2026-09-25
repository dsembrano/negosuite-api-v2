using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class ContactPhone
    {
        public int Id { get; set; }
        public int ContactId { get; set; }
        public string ContactNumber { get; set; }
        public int ContactNumberTypeId { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? LastUpdatedDate { get; set; }
        public int? CreatedByUserId { get; set; }
        public int? LastUpdatedByUserId { get; set; }

        public virtual Contact Contact { get; set; }
    }
}
