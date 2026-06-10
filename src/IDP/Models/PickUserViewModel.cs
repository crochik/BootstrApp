using System;
using System.Collections.Generic;

namespace IDP.Models;

public class PickUserViewModel
{
    public string ReturnUrl { get; set; }
    public string Provider { get; set; }
    public IList<UserEntry> Users { get; set; }

    public class UserEntry
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Description { get; set; }
    }
}

public class PickUserInputModel
{
    public string ReturnUrl { get; set; }
    public string Provider { get; set; }
    public Guid SelectedUserId { get; set; }
}
