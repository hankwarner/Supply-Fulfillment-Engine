using System;
using System.Collections.Generic;
using System.Text;

namespace FergusonSourcingCore.Models
{
    public class Requirement
    {
        public Requirement(string field, string value)
        {
            field = Field;
            value = Value;
        }

        public string Field { get; set; }

        public string Operator { get; set; }

        public string Value { get; set; }
    }
}
