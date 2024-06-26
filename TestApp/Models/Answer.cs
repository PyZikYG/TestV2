using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestApp.Models
{
    public abstract class Answer
    {
        public int Id { get; set; }

        //public int scoreTestNow { get; set; }
        public int QuestionId { get; set; }

        [Required] public Question Question { get; set; }

        [Required] public string AnswerType { get; set; }

        [Required] public float Score { get; set; }

        [Required] public TestResult TestResult { get; set; }

        [Required] public ushort Order { get; set; }

        public int? OptionId { get; set; }
        
        public Option Option { get; set; }

        public AnswerResult? Result { get; set; }
    }

    public enum AnswerResult : int
    {
        Wrong,
        PartiallyRight,
        Right,
    }

    public class SingleChoiceAnswer : Answer
    {
    }

    public class MultiChoiceAnswer : Answer
    {
        public IList<AnswerOption> AnswerOptions { get; set; }
    }

    public class TextAnswer : Answer
    {
        public string Text { get; set; }
    }

    public class DragAndDropAnswer : Answer
    {
        public List<DragAndDropAnswerOption> DragAndDropAnswerOptions { get; set; }
    }

    public class CodeAnswer : Answer
    {
        public Code Code { get; set; }
    }

    public class DragAndDropAnswerOption
    {
        public int Id { get; set; }
        public int AnswerId { get; set; }
        public DragAndDropAnswer Answer { get; set; }
        public int OptionId { get; set; }
        public Option Option { get; set; }
        public int RightOptionId { get; set; }
        public Option RightOption { get; set; }
        public int ChosenOrder { get; set; }
        
    }

    public class AnswerOption
    {
        public int AnswerId { get; set; }
        public MultiChoiceAnswer Answer { get; set; }
        public int OptionId { get; set; }
        public Option Option { get; set; }
        public bool Checked { get; set; }
    }
}