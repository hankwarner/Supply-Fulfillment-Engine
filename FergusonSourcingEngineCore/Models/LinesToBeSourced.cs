using System;
using System.Collections.Generic;
using System.Text;

namespace FergusonSourcingCore.Models
{
    public class LinesToBeSourced
    {
        public List<SingleLine> CurrentLines = new List<SingleLine>();

        public List<SingleLine> UnsourcedLines = new List<SingleLine>();

        public List<SingleLine> SourcedLines = new List<SingleLine>();
    }
}
