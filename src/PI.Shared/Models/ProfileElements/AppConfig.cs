using System;

namespace PI.Shared.Models
{
    public class AppConfig
    {
        public Guid AccountId { get; set; }
        public string Name { get; set; }
        public string Menu { get; set; }
        public string InitialPage { get; set; }
        public bool DisableHistory { get; set; }
    }
}