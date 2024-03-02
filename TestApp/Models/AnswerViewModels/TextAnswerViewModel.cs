using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestApp.Models.AnswerViewModels
{
    public class TextAnswerViewModel
    {
        [Required]
        public string Text { get; set; }
    }
}
