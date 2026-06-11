namespace blazorBookLibrary.Shared.Security

open System.Threading.Tasks

type IBotScoreService =
    abstract member GetBotScoreAsync: token: string -> Task<double>
    abstract member ApplyBotDelayAsync: score: double -> Task
