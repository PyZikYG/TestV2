using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TestApp.Models.AnswerViewModels
{
    public class MultiChoiceAnswerViewModel
    {
        [Required] 
        public List<int> CheckedOptionIds { get; set; }
    }
}