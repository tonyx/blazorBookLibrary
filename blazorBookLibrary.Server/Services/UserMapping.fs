
namespace BookLibrary.Services

open blazorBookLibrary.Data
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons

module UserMapping =
    let toAppUserInfo (appUser: ApplicationUser) =
        {
            Id = appUser.Id
            UserName = appUser.UserName
            Email = appUser.Email
            CodiceFiscale = appUser.CodiceFiscale
            IsIdentifiedPhysically = appUser.IsIdentifiedPhysically
            PhoneNumber = appUser.PhoneNumber
            Nome = appUser.Nome
            Cognome = appUser.Cognome
        }
