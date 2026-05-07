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
    abstract member CreateUserAsync: context: UserContext * user:User * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetUserAsync: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<User, string>>
    abstract member GetUserDetailsAsync: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<UserDetails, string>>
    abstract member SetFiscalCodeAsync: context: UserContext * userId:UserId * fiscalCode:FiscalCode * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetNameAsync: context: UserContext * userId:UserId * name:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetSurnameAsync: context: UserContext * userId:UserId * surname:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetPhoneNumberAsync: context: UserContext * userId:UserId * phoneNumber:PhoneNumber * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetIsPhysicallyIdentifiedAsync: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member UnSetIsPhysicallyIdentifiedAsync: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GhostUserAsync: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetUser: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<User, string>>
    abstract member SetAppUserInfoAsync: context: UserContext * userId:UserId * appUserInfo:AppUserInfo * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetDistributionPointsManagedByUserAsync: context: UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<DistributionPoint>, string>>

    