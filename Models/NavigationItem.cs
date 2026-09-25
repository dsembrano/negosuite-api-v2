using System;
using System.Collections.Generic;

namespace negosuite_api.Models
{
    public class NavigationItem
    {
        public NavigationItem() {
            //Children = new HashSet<NavigationItem>();
        }

        public int Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Type { get; set; }
        public string Icon { get; set; }
        public string Link { get; set; }
        public int? ParentId { get; set; }
        public NavigationItem Parent { get; set; }
        public ICollection<NavigationItem> Children { get; set; }
    }
}
