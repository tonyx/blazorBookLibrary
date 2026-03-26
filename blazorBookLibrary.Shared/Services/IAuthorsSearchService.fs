namespace BookLibrary.Shared.Services

open System.Threading.Tasks

type AuthorMetadata = {
    Name: string
    Isni: Option<string>
}

type IAuthorsSearchService =
    abstract member LookupByNameAsync : name: string -> Task<Result<AuthorMetadata, string>>