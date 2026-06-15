using System;
using System.Collections.Generic;

namespace AI_Interviwer.Models;

public partial class InterviewSession
{
    public int SessionId { get; set; }

    public int? UserId { get; set; }

    public string Role { get; set; } = null!;

    public string Level { get; set; } = null!;

    public int TotalLength { get; set; }

    public string InterviewType { get; set; } = null!;

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual InterviewResult? InterviewResult { get; set; }

    public virtual ICollection<SessionQuestion> SessionQuestions { get; set; } = new List<SessionQuestion>();

    public virtual User? User { get; set; }
}
