using System;

namespace TimeBase.Supervisor.Entities
{
    public class Symbol
    {
        public Guid Id { get; set; }
        public string SymbolValue { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Exchange { get; set; }
        public string Currency { get; set; }
        public string Metadata { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
