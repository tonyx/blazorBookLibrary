namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IUserService = 
    abstract member CreateUserAsync: user:User * ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetUserAsync: userId:UserId * ?ct:CancellationToken -> Task<Result<User, string>>
    