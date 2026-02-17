using ApplesoftEmulator;

Console.Title = "Applesoft BASIC Emulator";

// Display startup banner
Console.WriteLine();
Console.WriteLine("                APPLESOFT BASIC EMULATOR");
Console.WriteLine("          Based on Apple ][ Applesoft BASIC");
Console.WriteLine();
Console.WriteLine("  Type BASIC commands or line-numbered programs.");
Console.WriteLine("  Commands: RUN, LIST, NEW, SAVE \"file\", LOAD \"file\"");
Console.WriteLine("  Type QUIT or EXIT to leave.");
Console.WriteLine();
Console.WriteLine("]");

// Entry point for the Applesoft BASIC Emulator application.
var interpreter = new Interpreter();
// The tokenizer instance used for parsing user input.
var tokenizer = new Tokenizer();

// Main REPL loop for the Applesoft BASIC Emulator.
while (true)
{
    Console.Write("]");
    string? input = Console.ReadLine();

    if (input == null) break;
    input = input.TrimEnd();
    if (string.IsNullOrWhiteSpace(input)) continue;

    // Exit commands
    if (input.Equals("QUIT", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    try
    {
        // Check if line starts with a line number (program entry)
        string trimmed = input.TrimStart();
        if (trimmed.Length > 0 && char.IsDigit(trimmed[0]))
        {
            int i = 0;
            while (i < trimmed.Length && char.IsDigit(trimmed[i])) i++;
            if (int.TryParse(trimmed[..i], out int lineNum))
            {
                string rest = trimmed[i..].TrimStart();
                if (string.IsNullOrEmpty(rest))
                {
                    // Just a line number = delete that line
                    interpreter.StoreLine(lineNum, "");
                }
                else
                {
                    interpreter.StoreLine(lineNum, rest);
                }
                continue;
            }
        }

        // Direct mode execution
        interpreter.ExecuteDirect(input.ToUpper() == "RUN" ? "RUN" : input);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"?ERROR: {ex.Message}");
    }
}

Console.WriteLine();
Console.WriteLine("GOODBYE.");
