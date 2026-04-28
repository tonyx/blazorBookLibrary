
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
            CodiceFiscale = if appUser.CodiceFiscale = null then "" else appUser.CodiceFiscale
            IsIdentifiedPhysically = appUser.IsIdentifiedPhysically
            PhoneNumber = if appUser.PhoneNumber = null then "" else appUser.PhoneNumber
            Nome = if appUser.Nome = null then "" else appUser.Nome
            Cognome = if appUser.Cognome = null then "" else appUser.Cognome
        }
