using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TestApp.Models.TestViewModels;

namespace TestApp.Models
{
    public class Test
    {
        public enum QuestionTypeEnum
        {
            [Display(Name = "С одним правильным ответом")]
            SingleChoiceQuestion = 1,
            [Display(Name = "С несколькими правильными ответами")]
            MultiChoiceQuestion = 2,
            [Display(Name = "С вводом текста")] 
            TextQuestion = 3,
            [Display(Name = "На восстановление последовательности")]
            DragAndDropQuestion = 4,
            [Display(Name = "На написание кода")] 
            CodeQuestion = 5
        }

        [Key] public int Id { get; set; }

        [Required] public string Name { get; set; }

        public int CreatedById { get; set; }

        [Required] public User CreatedBy { get; set; }

        [Required]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public bool IsEnabled { get; set; }

        [Required] public bool IsDeleted { get; set; } = false;

        public bool Shuffled { get; set; }

        public int TimeToPassing { get; set; }

        public bool HideRightAnswers { get; set; } = false;
        public ICollection<Question> Questions { get; set; }
        
        public ICollection<TestResult> TestResults { get; set; }
        public int Count { get; set; } = 0;
    }

    public class AddTestModel
    {
        public List<Test> Model1 { get; set; }
        public AddTestViewModel Model2 { get; set; }
    }

    public class TestResultsModel
    {
        public Test Test { get; set; }
        public List<TestResult> Results { get; set; }
    }
}