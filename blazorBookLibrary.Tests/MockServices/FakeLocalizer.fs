namespace blazorBookLibrary.Tests.MockServices

open Microsoft.Extensions.Localization
open blazorBookLibrary.Shared.Resources
open System

type FakeLocalizer<'T>() =
    interface IStringLocalizer<'T> with
        member this.Item with get(name: string) = LocalizedString(name, name)
        member this.Item with get(name: string, [<ParamArray>] arguments: obj[]) = LocalizedString(name, name)
        member this.GetAllStrings(includeParentCultures) = Seq.empty
