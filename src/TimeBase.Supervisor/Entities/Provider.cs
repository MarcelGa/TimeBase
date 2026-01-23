using System;

namespace TimeBase.Supervisor.Entities
{
    public class Provider
    {
        public Guid Id { get; set; }
        public string Slug { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public bool Enabled { get; set; } = true;
        public string RepositoryUrl { get; set; }
        public string ImageUrl { get; set; }
        public string Config { get; set; }
        public string Capabilities { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
