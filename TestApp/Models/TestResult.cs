using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TestApp.Models
{
    public class TestResult
    {
        [Key] public int Id { get; set; }

        [Required] public bool IsCompleted { get; set; }

        public DateTime CompletedOn { get; set; }

        public DateTime StartedOn { get; set; }

        public ICollection<Answer> Answers { get; set; }

        public uint RightAnswersCount { get; set; }

        public float TotalScore { get; set; } = 0;

        public float TempScoreNow { get; set; } = 0;

        [Required] public uint TotalQuestions { get; set; }

        public int CompletedByUserId { get; set; }

        [Required] public User CompletedByUser { get; set; }

        public int TestId { get; set; }
        [Required] public Test Test { get; set; }

        [NotMapped]
        public TimeSpan TimeTaken
        {
            get => CompletedOn - StartedOn;
        }
    }
}