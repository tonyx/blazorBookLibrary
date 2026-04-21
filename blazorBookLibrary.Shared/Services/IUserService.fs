namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IUserService = 
    abstract member CreateUserAsync: user:User * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetUserAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<User, string>>
    abstract member GetUserDetailsAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<UserDetails, string>>
    abstract member SetFiscalCodeAsync: userId:UserId * fiscalCode:FiscalCode * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetNameAsync: userId:UserId * name:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetSurnameAsync: userId:UserId * surname:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetPhoneNumberAsync: userId:UserId * phoneNumber:PhoneNumber * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetIsPhysicallyIdentifiedAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member UnSetIsPhysicallyIdentifiedAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GhostUserAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    // abstract member AddReviewOfBookAsync: userId:UserId * bookId:BookId * comment:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>

    