namespace BookLibrary.Shared.Services

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared
open BookLibrary.Shared.Commons

[<CLIMutable>]
type LibrarianChatMessage =
    {
        Role: string
        Content: string
        Timestamp: DateTime
    }

[<CLIMutable>]
type LibrarianChatRequest =
    {
        Message: string
        ConversationHistory: List<LibrarianChatMessage>
        IncludeGeneralKnowledge: bool
        ScopeBookIds: Option<List<BookId>>
        ForceNewSearch: bool
    }

[<CLIMutable>]
type LibrarianChatResponse =
    {
        AnswerMarkdown: string
        ReferencedBooks: List<BookSearchResult>
        ScopeBookIds: List<BookId>
    }

type ILibrarianChatService =
    abstract member ChatAsync: context: UserContext * request: LibrarianChatRequest * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<LibrarianChatResponse, string>>
