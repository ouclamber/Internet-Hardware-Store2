using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;

namespace REACT_ASP.Models
{
    public class SignUpRequest
    {
        public required string UserName { get; set; }
        public required string Password { get; set; }
        public required string ConfirmPassword { get; set; }
        public required string Role { get; set; }
    }

    public class SignUpResponse
    {
        public SignUpResponse()
        {
            Message = string.Empty;
            Errors = new List<string>(); 
        }
        
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }  
    }
}