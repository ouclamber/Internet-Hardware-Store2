using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace REACT_ASP.Model
{
    public class SignInRequest
    {
        [Required]
        public required string UserName { get; set; }

        [Required]
        public required string Password { get; set; }

        [Required]
        public required string Role { get; set; }
    }

    public class SignInResponse
    {
        public SignInResponse()
        {
            Message = string.Empty;
            Errors = new List<string>();  
        }
        
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }  
    }
}