using System;
using System.Collections.Generic;

#nullable disable

namespace negosuite_api.Models
{
    public partial class AgingPeriod
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool ShowCurrent { get; set; }
        public short Period1 { get; set; }
        public short Period2 { get; set; }
        public short Period3 { get; set; }
        public short Period4 { get; set; }
    }
}
