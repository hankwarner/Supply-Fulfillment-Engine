using System;
using System.Collections.Generic;
using System.Text;

namespace FergusonSourcingCore.Models
{
    public class TrilogieRequest
    {
        public TrilogieStatus TrilogieStatus { get; set; }

        public string TrilogieErrorMessage { get; set; } = "";

        public string TrilogieOrderId { get; set; } = "";
    }

    public enum TrilogieStatus { Pass, Fail }
}
