using System;
using System.Collections.Generic;

namespace AI_Interviwer.Models;

public partial class User
{
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<InterviewSession> InterviewSessions { get; set; } = new List<InterviewSession>();
}
