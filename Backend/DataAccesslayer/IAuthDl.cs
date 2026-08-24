using REACT_ASP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using REACT_ASP.Models;
using REACT_ASP.Model;

namespace REACT_ASP.DataAccesslayer;
public interface IAuthDl
{
    public Task<SignUpResponse> SignUp(SignUpRequest request);
    public Task<SignInResponse> SignIn(SignInRequest request);
}


