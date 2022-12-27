// Peg 15 puzzle goofiness
// Lomont 2022
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

/* board:
               14
             12   13
           9   10   11 
         5   6    7    8
       0   1    2    3   4

store a board as a an array, with slot j holding 1 for set, else 0 for empty
store a move as (src,mid,dst) tuple meaning can jump from src, over mid, into dst 

*/

int dimension = 2; // todo - allow more than 2d,3d
int sideLength = 5;

// get size of board and move list
var (boardSize, moveList) = GenMoves(sideLength, dimension);

bool hashSolutions = true; // hash for speed

Console.WriteLine($"Side length {sideLength}, dimension {dimension}, boardsize {boardSize} pegs");


// track maps of move graphs
List<(int removed, Dictionary<string, List<string>> nodeMap)> nodeMaps = new();

// solve with each missing peg # 
for (var removePiece = 0; removePiece < boardSize; ++removePiece)
{
    var (totalSolutions, nodes, elapsedMs) = Solve(removePiece);
    Console.WriteLine($"remove {removePiece}, total solutions {totalSolutions} in {elapsedMs} ms");
    nodeMaps.Add((removePiece, nodes));
}

if (false)
{ // show intersection info
    for (var i = 0; i < nodeMaps.Count; ++i)
    {
        var hi = new HashSet<string>();
        hi.UnionWith(nodeMaps[i].nodeMap.Keys);
        Console.WriteLine($"Remove {nodeMaps[i].removed} has graph size {hi.Count}");
        for (var j = i + 1; j < nodeMaps.Count; ++j)
        {
            var hj = new HashSet<string>();
            hj.UnionWith(nodeMaps[j].nodeMap.Keys);
            Console.Write($"  Intersect with remove {nodeMaps[i].removed} (graph size {hj.Count}) leaves ");
            hj.IntersectWith(hi);
            Console.WriteLine($"{hj.Count} nodes");
        }
    }
}

if (false)
{ // some stuff to generate graph info for Mathematica

    // merge all dicts for total graph
    HashSet<(string parent, string child)> edges = new();
    HashSet<string> verts = new();

    foreach (var d in nodeMaps)
        foreach (var p in d.nodeMap)
        {
            verts.Add(p.Key);
            foreach (var v in p.Value)
            {
                verts.Add(v);
                edges.Add((p.Key, v));
            }
        }

    DumpGraph(verts,edges);
}




/* create move table
Idea: turn original image on side, relabel

        0
      1  2
     3  4  5
   6  7  8  9
 10 11 12 13 14

            14
         12   13
       9   10   11 
     5   6    7    8
   0   1    2    3   4

left align into grid

| 14 | 
| 12 | 13 |  
|  9 | 10 | 11 |
|  5 |  6 |  7 | 8 |
|  0 |  1 |  2 | 3 | 4 | 

Let side length be s = 5
the grid is (i,j) for 0 <= i < s, 0 <= j < s-i  (A)

| 0,4 | 
| 0,3 | 1,3 |  
| 0,2 | 1,2 | 2,2 |
| 0,1 | 1,1 | 2,1 | 3,1 |
| 0,0 | 1,0 | 2,0 | 3,0 | 4,0 | 


(i,j) maps to cell # via c = i + (j*s) - j*(j-1)/2
cell to (i,j) is messy, just inverse table or subtract largest form of j term, i is remainder

legal moves are of form (if all in bounds (A) )
    (i,j)-> (i ± 2,j)
    (i,j)-> (i,j ± 2)
    (i,j)-> (i± 2,j ∓ 2)# note signs must differ!

This generalizes to higher dimensions as follows:
legal coords have manhattan distance from origin < sideLength
possible moves are single axis aligned ±2 OR
two coords of the N moved by ± and ∓ as above.

*/

(int boardLise, IList<(int src, int mid, int dst)> moveList) 
    GenMoves(int sideLength, int dimension)
{
    // tuple to cell map
    Dictionary<string, int> cellMap = new();
    int cellIndex = 0;
    foreach (var coord in TupleCounter(sideLength, dimension))
        cellMap.Add(HashTuple(coord), cellIndex++);

    var moves = new List<(int src, int mid, int dst)>();
    var s = sideLength;
    foreach (var src in TupleCounter(sideLength,dimension))
        foreach (var dst in DestMoves(src))
            AddIfLegal(src, dst);

    return (cellMap.Count, moves);

    string HashTuple(IList<int> tuple) => tuple.Aggregate("", (a, b) => (a + "," + b));


    void AddIfLegal(IList<int> src, IList<int> dst)
    {
        if (Legal(src) && Legal(dst))
            moves.Add((ToCell(src), ToCell(Mid(src,dst)),ToCell(dst)));

        bool Legal(IList<int> p) => p.All(c => c >= 0) && p.Sum() < sideLength;

        IList<int> Mid(IList<int> p1, IList<int> p2)
        {
            // do not modify original
            var dst = p1.ToList();
            for (var i = 0; i < dst.Count; ++i)
                dst[i] = (p1[i] + p2[i])/ 2;
            return dst;
        }
    }

    // 2d case
    //int ToCell((int i, int j) p) => p.i + p.j * s - p.j * (p.j - 1) / 2;
    int ToCell(IList<int> coord) => cellMap[HashTuple(coord)];
}

// gen moves to check from a coord
IEnumerable<IList<int>> DestMoves(IList<int> coord)
{
    var dim = coord.Count;
    // single axis
    for (var i = 0; i < dim; ++i)
    {
        yield return Change1(i, 2);
        yield return Change1(i, -2);
    }
    // double axis
    for (var i = 0; i < dim; ++i)
    for (var j = 0; j < dim; ++j)
    {
        if (i == j) continue; // do not do this one
        yield return Change2(i, j, 2);
        //yield return Change2(i, j, -2);
    }

    IList<int> Change1(int index, int delta)
    {
        // do not modify original
        var dst = coord.ToList();
        dst[index] += delta;
        return dst;
    }
    IList<int> Change2(int index1, int index2, int delta)
    {
        // do not modify original
        var dst = coord.ToList();
        dst[index1] += delta;
        dst[index2] -= delta;
        return dst;
    }
}



// count all legal tuples (x0,x1,x2,..x_{dim-1})
// these are all the tuples up to manhattan dist < side length
IEnumerable<IList<int>> TupleCounter(int sideLength, int dimension)
{
    // all zeroes
    var tuple = new int[dimension]; 

    bool done = false;
    while (!done)
    {
        // brute force, bad as dimensions increase, ok for now
        if (tuple.Sum()<sideLength)
            yield return tuple;



        // incr left most side, roll up as carries over
        int index = 0;
        while (index < dimension)
        {
            tuple[index]++;
            // each "digit" can go from 0 to side length - sum of prev "digits"
            if (tuple[index] >= sideLength)
                tuple[index++] = 0;
            else 
                break; // ok next tuple value
        }
        done |= index >= dimension; // odometer rolled over
    }
}


// do or undo move on board by toggling values between 0 and 1
void ToggleMove(int[] board, (int src, int mid, int dst) move)
{
    // move a, jump b, to c
    board[move.src] ^= 1; // clear/set src
    board[move.mid] ^= 1; // clear/set mid
    board[move.dst] ^= 1; // set/clear dst
}

// return true if move on given board is legal
bool IsLegal(int[] board, (int a, int b, int c) move) => board[move.a] == 1 && board[move.b] == 1 && board[move.c] == 0;

// dump board
void Dump(int[] board)
{
    var ind = 0;
    for (var j = 0; j < sideLength; ++j)
    {
        for (var i = 0; i <= j; ++i)
        {
            Console.Write($"{board[ind++]} ");
        }

        Console.WriteLine();
    }
}

// turn board into string
// string is hexadecimal number of board bits
string BoardHash(int[] board)
{
    var sb = new StringBuilder();
    var val = 0;
    for (var i = 0; i < board.Length; ++i)
    {
        val = val * 2 + board[i];
        if ((i % 4) == 3)
        {
            Append(val);
            val = 0;
        }
    }
    // get last one!
    Append(val);

    void Append(int val) => sb.Append("0123456789ABCDEF"[val]);

    return sb.ToString();
}

// search all positions starting at given board position
// depth is count of moves made so far
// return count of solutions from given position
long Recurse(
    int[] board,
    int depth, // current move depth 0+
    int[] movesMade, // track moves made
    Action<IList<int>> solveAction, // what to do with move list on a solution found
    Action<string, string> moveAction, // what to do when about to move from parent to child
    Dictionary<string, long>? solnCountHash // store sub-solutions if not null
)
{
    long solutions = 0;
    var parentHash = BoardHash(board);
    if (solnCountHash==null || !solnCountHash.ContainsKey(parentHash))
    {
        if (depth == board.Length - 2)
        {
            solveAction(movesMade);
            solutions = 1;
        }
        else
        {

            // moves start every three positions
            foreach (var move in moveList)
            {
                if (IsLegal(board, move))
                {
                    // move is legal. Do it, and look deeper
                    ToggleMove(board, move);
                    movesMade[depth] = move.dst; // save path so far
                    var childHash = BoardHash(board);
                    moveAction(parentHash, childHash);
                    solutions += Recurse(board, depth + 1, movesMade, solveAction, moveAction, solnCountHash); // check sub-positions from this one
                    ToggleMove(board, move); // restore board to get ready to try the next sub position
                }
            }
        }
        if (solnCountHash != null)
            solnCountHash.Add(parentHash, solutions);
    }
    return solnCountHash==null?solutions:solnCountHash[parentHash];
}


// find all solutions for the given peg missing
// return total solutions and map of parent to all child moves
(long totalSolutions, Dictionary<string, List<string>> nodes, long elapsedMs) 
    Solve(int missingPegIndex)
{
    // store moves made, at move # k store move index
    var movesMade = new int[boardSize-2];
    Dictionary<string, List<string>> nodes = new(); // for each parent state, store child states
    Dictionary<string, long> solnCountHash = new(); // speed up searches

    // set up board
    var board = new int[boardSize];
    for (int i = 0; i < boardSize; ++i)
        board[i] = 1;
    board[missingPegIndex] = 0;

    var sw = new Stopwatch();
    sw.Start();
    // and search it, depth = 0
    var totalSolutions = Recurse(board, 0, movesMade,
        soln =>
        {
            //++totalSolutions; // count solutions
            //if ((totalSolutions%100)==0)
                //Console.WriteLine($"Soln ");
            //Dump(board); // could dump board to check is empty
            // could dump or save moves
        },
        (parentHash, childHash) =>
        {
            if (!nodes.ContainsKey(parentHash))
                nodes.Add(parentHash, new());
            nodes[parentHash].Add(childHash);
        },
        hashSolutions?solnCountHash:null
    );
    sw.Stop();
    return (totalSolutions, nodes, sw.ElapsedMilliseconds);
}

void DumpGraph(HashSet<string> verts, HashSet<(string parent, string child)> edges)
{
    // for Mathematica
    Console.WriteLine("g = Graph[{");
    var f = true;
    foreach (var v in verts)
    {
        var c = f ? "" : ",";
        Console.Write($"{c}\"{v}\"");
        f = false;
    }

    Console.WriteLine("},{");
    f = true;
    foreach (var (src, dst) in edges)
    {
        var c = f ? "" : ",";
        Console.Write($"{c}\"{src}\"->\"{dst}\"");
        f = false;
    }
    Console.WriteLine("},VertexLabels->\"Name\"];");
}

// END OF FILE


