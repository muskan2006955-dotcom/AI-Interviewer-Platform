using System;
using System.Collections.Generic;

namespace AI_Interviwer.Models;

public partial class SessionQuestion
{
    public int QuestionId { get; set; }

    public int? SessionId { get; set; }

    public string QuestionText { get; set; } = null!;

    public string? UserCodeAnswer { get; set; }

    public string? UserTextAnswer { get; set; }

    public string? AiFeedback { get; set; }

    public bool? IsCorrect { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int Score { get; set; }

    public virtual InterviewSession? Session { get; set; }
}
