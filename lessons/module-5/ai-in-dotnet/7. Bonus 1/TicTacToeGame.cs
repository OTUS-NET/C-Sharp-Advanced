
// === Игровая логика ===
class TicTacToeGame
{
    private char[] board = new char[9];

    public TicTacToeGame()
    {
        for (int i = 0; i < 9; i++)
            board[i] = (char)('1' + i);
    }

    public bool MakeMove(int position, char symbol)
    {
        if (position < 1 || position > 9) return false;
        if (board[position - 1] == 'X' || board[position - 1] == 'O') return false;

        board[position - 1] = symbol;
        return true;
    }

    public string GetBoardState()
    {
        return $@"
 {board[0]} | {board[1]} | {board[2]} 
-----------
 {board[3]} | {board[4]} | {board[5]} 
-----------
 {board[6]} | {board[7]} | {board[8]} 
";
    }

    public char? CheckWinner()
    {
        int[][] winPatterns = {
            new[] {0,1,2}, new[] {3,4,5}, new[] {6,7,8}, // ряды
            new[] {0,3,6}, new[] {1,4,7}, new[] {2,5,8}, // колонки
            new[] {0,4,8}, new[] {2,4,6}  // диагонали
        };

        foreach (var pattern in winPatterns)
        {
            if (board[pattern[0]] == board[pattern[1]] &&
                board[pattern[1]] == board[pattern[2]])
            {
                if (board[pattern[0]] == 'X' || board[pattern[0]] == 'O')
                    return board[pattern[0]];
            }
        }

        // Проверка на ничью
        if (board.All(c => c == 'X' || c == 'O'))
            return 'D'; // Draw

        return null;
    }

    public List<int> GetAvailableMoves()
    {
        return board
            .Select((c, i) => new { c, i })
            .Where(x => x.c != 'X' && x.c != 'O')
            .Select(x => x.i + 1)
            .ToList();
    }
}