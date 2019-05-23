using System;
using System.Collections.Generic;
using System.Text;

namespace PluginStructure.Infrastructure
{
    public class BaseEntity
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime ModifiedDateTime { get; set; }
    }
}
