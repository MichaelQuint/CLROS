using OS.Tooling;

return ToolingBuildInputs.RequireCurrent(args, Console.Error) ? ToolCommand.Run(args) : 1;
