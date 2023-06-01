using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RNG = UnityEngine.Random;
using SimonSpells;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;

public class SimonSpellsScript : MonoBehaviour
{
    [SerializeField]
    private MeshRenderer[] _screens;
    [SerializeField]
    private KMSelectable[] _buttons;
    [SerializeField]
    private TextMesh[] _cbText;

    private List<char> _stagesDone = new List<char>(4);
    private int _id = ++_idc;
    private static int _idc;
    private IEnumerator<char[]> _generator;
    private Position? _buffer;
    private ScreenColor[] layout;
    private bool _isSolved;

    private static readonly char[][] s_letterTable = new char[][]
    {
        new char[] { 'A', 'B', 'C', 'D', 'E' },
        new char[] { 'F', 'G', 'H', 'I', 'K' },
        new char[] { 'L', 'M', 'N', 'O', 'P' },
        new char[] { 'Q', 'R', 'S', 'T', 'U' },
        new char[] { 'V', 'W', 'X', 'Y', 'Z' }
    };
    private static readonly Dictionary<char, Vector2Int> s_reverseLetterTable =
        Enumerable
        .Range(0, 25)
        .Select(i => new Vector2Int(i / 5, i % 5))
        .ToDictionary(v => s_letterTable[v.y][v.x], v => v);

    private void Start()
    {
#if UNITY_EDITOR
        Predicate<string> isValid = word =>
        {
            var a = s_reverseLetterTable[word[0]];
            var b = s_reverseLetterTable[word[1]];
            var c = s_reverseLetterTable[word[2]];
            int x, y;

            if (a.x == b.x || a.x == c.x)
                x = a.x;
            else if (b.x == c.x)
                x = b.x;
            else
            {
                int[] j = new int[] { a.x, b.x, c.x };
                if (j.Contains(0) && j.Contains(1) && j.Contains(2))
                    x = 1;
                else if (j.Contains(1) && j.Contains(2) && j.Contains(3))
                    x = 2;
                else if (j.Contains(2) && j.Contains(3) && j.Contains(4))
                    x = 3;
                else if (j.Contains(3) && j.Contains(4) && j.Contains(0))
                    x = 4;
                else if (j.Contains(4) && j.Contains(0) && j.Contains(1))
                    x = 5;
                else if (j.Contains(0) && j.Contains(1) && j.Contains(3))
                    x = 3;
                else if (j.Contains(1) && j.Contains(2) && j.Contains(4))
                    x = 4;
                else if (j.Contains(2) && j.Contains(3) && j.Contains(0))
                    x = 0;
                else if (j.Contains(3) && j.Contains(4) && j.Contains(1))
                    x = 1;
                else if (j.Contains(4) && j.Contains(0) && j.Contains(2))
                    x = 2;
                else
                    throw new UnreachableException();
            }
            if (a.y == b.y || a.y == c.y)
                y = a.y;
            else if (b.y == c.y)
                y = b.y;
            else
            {
                int[] j = new int[] { a.y, b.y, c.y };
                if (j.Contains(0) && j.Contains(1) && j.Contains(2))
                    y = 1;
                else if (j.Contains(1) && j.Contains(2) && j.Contains(3))
                    y = 2;
                else if (j.Contains(2) && j.Contains(3) && j.Contains(4))
                    y = 3;
                else if (j.Contains(3) && j.Contains(4) && j.Contains(0))
                    y = 4;
                else if (j.Contains(4) && j.Contains(0) && j.Contains(1))
                    y = 5;
                else if (j.Contains(0) && j.Contains(1) && j.Contains(3))
                    y = 3;
                else if (j.Contains(1) && j.Contains(2) && j.Contains(4))
                    y = 4;
                else if (j.Contains(2) && j.Contains(3) && j.Contains(0))
                    y = 0;
                else if (j.Contains(3) && j.Contains(4) && j.Contains(1))
                    y = 1;
                else if (j.Contains(4) && j.Contains(0) && j.Contains(2))
                    y = 2;
                else
                    throw new UnreachableException();
            }

            return s_reverseLetterTable[word[3]].x == x || s_reverseLetterTable[word[3]].y == y;
        };
        Log(Words.WordList.Where(w => !isValid(w)).Join("|"));
        Log(Enumerable
            .Range(0, 26)
            .Select(i => (char)('A' + i))
            .Select(c => c.ToString() + ":" + Words.WordList.Count(s => s[0] == c))
            .Join(","));
        Log(isValid("SPELL"));
        Log(s_reverseLetterTable['U']);
#endif

        _generator = GenerateStages();
        _generator.MoveNext();

        for (int i = 0; i < 5; i++)
        {
            int j = i;
            _buttons[i].OnInteract += () => { Press(j); return false; };
        }

        SetColorblind(GetComponent<KMColorblindMode>().ColorblindModeActive);
    }

    private void Press(int i)
    {
        StartCoroutine(Animate(_buttons[i]));

        if (_isSolved)
            return;

        if (_buffer == null)
        {
            _buffer = (Position)i;
            Logf("Pressed the {0} button.", _buffer);
            return;
        }
        Logf("Pressed the {0} button.", layout[i]);

        char input = s_letterTable[(int)layout[i]][(int)_buffer];
        _buffer = null;
        if (!_generator.Current.Contains(input))
        {
            Logf("You tried to input a {0}. This is invalid. Strike!", input);
            GetComponent<KMBombModule>().HandleStrike();
            _stagesDone.Clear();
            _generator = GenerateStages();
            _generator.MoveNext();
            return;
        }

        _stagesDone.Add(input);
        Logf("You input a {0}. Total so far: {1}", input, _stagesDone.Join(""));
        if (_generator.MoveNext())
            return;

        Log("Module solved. Good job!");
        SetScreens(Enumerable.Repeat(ScreenColor.Black, 5).ToArray());
        GetComponent<KMAudio>().PlaySoundAtTransform("Solve", transform);
        GetComponent<KMBombModule>().HandlePass();
        _isSolved = true;
    }

    private IEnumerator Animate(KMSelectable button)
    {
        GetComponent<KMAudio>().PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, button.transform);
        button.AddInteractionPunch(0.1f);
        //0.02663
        //0.0133
        float x = button.transform.localPosition.x;
        float z = button.transform.localPosition.z;
        float t = Time.time;
        const float d = 0.05f;
        while (Time.time - t < d)
        {
            button.transform.localPosition = new Vector3(x, Mathf.Lerp(0.02663f, 0.0133f, (Time.time - t) / d), z);
            yield return null;
        }
        t = Time.time;
        while (Time.time - t < d)
        {
            button.transform.localPosition = new Vector3(x, Mathf.Lerp(0.0133f, 0.02663f, (Time.time - t) / d), z);
            yield return null;
        }

        button.transform.localPosition = new Vector3(x, 0.02663f, z);
    }

    private IEnumerator<char[]> GenerateStages()
    {
        // Do it like this so that the relative distribution of first letters is maintained (also no X's (or J's))
        char letter1 = Words.WordList.PickRandom()[0];
        List<string> words = Words.WordList.Where(w => w[0] == letter1).ToList();

        // There are only 120 color layouts, so we just find any valid one for stages 1 and 2.
        layout = Enumerable
            .Range(0, 120)
            .OrderBy(_ => RNG.value)
            .Select(i => GetLayout(i))
            .First(arr => StageOne(arr) == letter1);

        Logf("[Stage 1] The screen colors are laid out as follows: {0}", layout.Join(", "));
        Logf("[Stage 1] The valid letter is {0}.", letter1);
        SetScreens(layout);

        yield return new char[] { letter1 };

        char letter2 = words.PickRandom()[1];

        Vector2Int center = Vector2Int.zero;
        layout = Enumerable
            .Range(0, 120)
            .OrderBy(_ => RNG.value)
            .Select(i => GetLayout(i))
            .First(arr =>
            {
                center = s_reverseLetterTable[_stagesDone[0]] + StageTwo(arr);
                center = new Vector2Int(center.x % 5, center.y % 5);
                return
                    s_reverseLetterTable[letter2].y == center.y &&
                    (
                        s_reverseLetterTable[letter2].x == center.x
                        || s_reverseLetterTable[letter2].x == (center.x + 1) % 5
                        || s_reverseLetterTable[letter2].x == (center.x + 4) % 5
                    );
            });

        char[] valid = new char[]
        {
            s_letterTable[center.y][center.x],
            s_letterTable[center.y][(center.x + 1) % 5],
            s_letterTable[center.y][(center.x + 4) % 5]
        }
        .OrderBy(c => c)
        .ToArray();

        Logf("[Stage 2] The screen colors are laid out as follows: {0}", layout.Join(", "));
        Logf("[Stage 2] Valid letters: {0}", valid.Join(", "));
        Logf("[Stage 2] Of these, these form no words: {0}", valid.Where(c => !words.Any(w => w[1] == c)).Join(", "));
        Logf("[Stage 2] Therefore, these are correct: {0}", valid.Where(c => words.Any(w => w[1] == c)).Join(", "));
        SetScreens(layout);

        yield return valid.Where(c => words.Any(w => w[1] == c)).ToArray();
        words = words.Where(w => w[1] == _stagesDone[1]).ToList();


        char letter3 = words.PickRandom()[2];
        center = s_reverseLetterTable[letter3];
        int[] latinSquare = new int[5] { -1, -1, -1, -1, -1 };
        latinSquare[center.y] = 4 - center.x; // Rotate
        Log(words.Join(","));
        Log(letter3);
        Log(latinSquare.Join(","));
        for (int j = 0; j < 4; j++)
            latinSquare[Array.IndexOf(latinSquare, -1)] = new int[] { 0, 1, 2, 3, 4 }.Where(i => !latinSquare.Contains(i)).PickRandom();
        Log(latinSquare.Join(","));
        layout = latinSquare.Cast<ScreenColor>().ToArray();
        valid = Enumerable
            .Range(0, 5)
            .Select(i => s_letterTable[i][4 - latinSquare[i]]) // Rotate
            .OrderBy(c => c)
            .ToArray();

        Logf("[Stage 3] The screen colors are laid out as follows: {0}", layout.Join(", "));
        Logf("[Stage 3] Valid letters: {0}", valid.Join(", "));
        Logf("[Stage 3] Of these, these form no words: {0}", valid.Where(c => !words.Any(w => w[2] == c)).Join(", "));
        Logf("[Stage 3] Therefore, these are correct: {0}", valid.Where(c => words.Any(w => w[2] == c)).Join(", "));
        SetScreens(layout);

        yield return valid.Where(c => words.Any(w => w[2] == c)).ToArray();
        words = words.Where(w => w[2] == _stagesDone[2]).ToList();

        Vector2Int alfa = s_reverseLetterTable[_stagesDone[0]];
        Vector2Int bravo = s_reverseLetterTable[_stagesDone[1]];
        Vector2Int charlie = s_reverseLetterTable[_stagesDone[2]];
        center = new Vector2Int(StageFour(alfa.x, bravo.x, charlie.x), StageFour(alfa.y, bravo.y, charlie.y));
        valid = s_reverseLetterTable
            .Keys
            .Where(c => s_reverseLetterTable[c].x == center.x || s_reverseLetterTable[c].y == center.y)
            .OrderBy(c => c)
            .ToArray();

        layout = Enumerable
            .Range(0, 5)
            .Cast<ScreenColor>()
            .OrderBy(_ => RNG.value)
            .ToArray();

        Logf("[Stage 4] The screen colors are laid out as follows: {0}", layout.Join(", "));
        Logf("[Stage 4] Valid letters: {0}", valid.Join(", "));
        Logf("[Stage 4] Of these, these form no words: {0}", valid.Where(c => !words.Any(w => w[3] == c)).Join(", "));
        Logf("[Stage 4] Therefore, these are correct: {0}", valid.Where(c => words.Any(w => w[3] == c)).Join(", "));
        SetScreens(layout);

        yield return valid.Where(c => words.Any(w => w[3] == c)).ToArray();
        string word = words.First(w => w[3] == _stagesDone[3]);

        layout = Enumerable
            .Range(0, 5)
            .Cast<ScreenColor>()
            .OrderBy(_ => RNG.value)
            .ToArray();

        Logf("[Stage 4] The screen colors are laid out as follows: {0}", layout.Join(", "));
        Logf("[Stage 4] All letters are valid.");
        Logf("[Stage 4] Of these, only {0} forms a word.", word[4]);
        SetScreens(layout);

        yield return new char[] { word[4] };
    }

    private static ScreenColor[] GetLayout(int i)
    {
        int ix;
        List<ScreenColor> colors = Enumerable.Range(0, 5).Cast<ScreenColor>().ToList();
        ScreenColor[] arr = new ScreenColor[5];
        for (int j = 0; j < 5; j++)
        {
            i = Math.DivRem(i, 5 - j, out ix);
            arr[j] = colors[ix];
            colors.RemoveAt(ix);
        }
        return arr;
    }

    private static char StageOne(ScreenColor[] arr)
    {
        // Notably, it is not possible to generate C, X, L, or P.
        int pos = Array.IndexOf(arr, ScreenColor.Black);
        pos =
            Array.IndexOf(arr, ScreenColor.Red) > pos
            ? pos + 1
            : pos - 1;
        pos =
            arr[pos] == ScreenColor.Yellow
            || arr[pos] == ScreenColor.Blue
            || arr[pos] == ScreenColor.Green
            ? pos
            : Array.IndexOf(arr, ScreenColor.Blue);

        int col = (int)arr[4];
        col =
            (int)arr[0] > col
            ? col + 1
            : col - 1;
        col =
            (int)arr[1] == col
            || (int)arr[2] == col
            || (int)arr[3] == col
            ? col
            : (int)arr[2];

        return s_letterTable[col][pos];
    }

    private static Vector2Int StageTwo(ScreenColor[] arr)
    {
        Vector2Int delta = Vector2Int.zero;
        if (arr[1] == ScreenColor.Green)
            delta.x += 4;
        if (arr[3] == ScreenColor.Yellow)
            delta.x += 1;
        if (arr[2] == ScreenColor.Black)
            delta.y += 4;
        if (arr[2] == ScreenColor.Red)
            delta.y += 1;
        if (arr[0] == ScreenColor.Blue)
            delta.y += 4;
        if (arr[4] == ScreenColor.Blue)
            delta.y += 1;
        return delta;
    }

    private int StageFour(int a, int b, int c)
    {
        if (a == b || a == c)
            return a;
        else if (b == c)
            return b;
        else
        {
            int[] j = new int[] { a, b, c };
            if (j.Contains(0) && j.Contains(1) && j.Contains(2))
                return 1;
            else if (j.Contains(1) && j.Contains(2) && j.Contains(3))
                return 2;
            else if (j.Contains(2) && j.Contains(3) && j.Contains(4))
                return 3;
            else if (j.Contains(3) && j.Contains(4) && j.Contains(0))
                return 4;
            else if (j.Contains(4) && j.Contains(0) && j.Contains(1))
                return 5;
            else if (j.Contains(0) && j.Contains(1) && j.Contains(3))
                return 3;
            else if (j.Contains(1) && j.Contains(2) && j.Contains(4))
                return 4;
            else if (j.Contains(2) && j.Contains(3) && j.Contains(0))
                return 0;
            else if (j.Contains(3) && j.Contains(4) && j.Contains(1))
                return 1;
            else if (j.Contains(4) && j.Contains(0) && j.Contains(2))
                return 2;
            else
                throw new UnreachableException();
        }
    }

    private void Logf(string s, params object[] args)
    {
        Log(string.Format(s, args));
    }

    private void Log(object s)
    {
        Debug.Log("[Simon Spells #" + _id + "] " + s.ToString());
    }

    #region Visual Helpers
    // These two enums are ordered to match the manual, allowing for unsafe manipulation.
    private enum ScreenColor
    {
        Red,
        Yellow,
        Blue,
        Green,
        Black
    }

    private enum Position
    {
        TL,
        TR,
        M,
        BL,
        BR
    }

    private void SetScreens(ScreenColor[] colors)
    {
        if (colors.Length != 5)
            throw new ArgumentException("Expected exactly 5 colors, got " + colors.Length, "colors");
        for (int i = 0; i < 5; i++)
            StartCoroutine(AnimateColor(_screens[i], _cbText[i], _screens[i].material.color, colors[i]));
    }

    private int _animCount = 0;
    private IEnumerator AnimateColor(Renderer r, TextMesh cb, Color start, ScreenColor finish)
    {
        _animCount++;
        StartCoroutine(FlickerCBText(cb, finish));
        yield return new WaitForSeconds(RNG.Range(0f, 1.6f));
        float duration = RNG.Range(1f, 1.6f);
        float t = Time.time;
        Color fcolor = EnumToColor(finish);
        while (Time.time - t < duration)
        {
            r.material.color = Color.Lerp(start, fcolor, (Time.time - t) / duration);
            yield return null;
        }
        r.material.color = fcolor;
        _animCount--;
    }

    private IEnumerator FlickerCBText(TextMesh cb, ScreenColor finish)
    {
        _animCount++;
        string t = cb.text;
        yield return new WaitForSeconds(RNG.Range(0f, 0.2f));
        cb.text = "";
        yield return new WaitForSeconds(RNG.Range(0f, 0.1f));
        cb.text = t;
        yield return new WaitForSeconds(RNG.Range(0f, 0.1f));
        cb.text = "";
        if (finish == ScreenColor.Black)
        {
            _animCount--;
            yield break;
        }
        yield return new WaitForSeconds(RNG.Range(1f, 1.8f));
        string t2 = finish.ToString().Substring(0, 1);
        cb.text = t2;
        yield return new WaitForSeconds(RNG.Range(0f, 0.1f));
        cb.text = "";
        yield return new WaitForSeconds(RNG.Range(0f, 0.1f));
        cb.text = t2;
        _animCount--;
    }

    private Color32 EnumToColor(ScreenColor screenColor)
    {
        switch (screenColor)
        {
            case ScreenColor.Black:
                return new Color32(100, 100, 100, 255);
            case ScreenColor.Blue:
                return new Color32(100, 100, 255, 255);
            case ScreenColor.Green:
                return new Color32(100, 255, 100, 255);
            case ScreenColor.Red:
                return new Color32(255, 100, 100, 255);
            case ScreenColor.Yellow:
                return new Color32(255, 255, 100, 255);
        }
        throw new UnreachableException();
    }

    private bool _colorblind;
    private void SetColorblind(bool on)
    {
        _colorblind = on;
        foreach (TextMesh m in _cbText)
            m.color = on ? Color.black : Color.clear;
    }
    #endregion

    [Serializable]
    private class UnreachableException : Exception
    {
        public UnreachableException()
        {
        }

        public UnreachableException(string message) : base(message)
        {
        }

        public UnreachableException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected UnreachableException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

#pragma warning disable 414
    private const string TwitchHelpMessage = "Use \"!{0} TL TR M BL BR\" to press each button in order. \"press\" is optional. Use \"!{0} colorblind/colourblind\" to toggle colorblind mode.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (command.EqualsIgnoreCase("colorblind") || command.EqualsIgnoreCase("colourblind") || command.EqualsIgnoreCase("cb"))
        {
            yield return null;
            SetColorblind(!_colorblind);
            yield break;
        }
        Match m;
        if ((m = Regex.Match(command, @"^\s*(?:press\s+)?((?:[BT][LR]|M)(?:\s+(?:[BT][LR]|M))*)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            yield return null;
            string[] commands = Regex.Split(m.Groups[1].Value, @"\s+");

            foreach (string co in commands)
            {
                string c = co.ToUpperInvariant();

                if (c == "TL")
                    _buttons[0].OnInteract();
                else if (c == "TR")
                    _buttons[1].OnInteract();
                else if (c == "M")
                    _buttons[2].OnInteract();
                else if (c == "BL")
                    _buttons[3].OnInteract();
                else if (c == "BR")
                    _buttons[4].OnInteract();
                else
                    continue;
                yield return new WaitForSeconds(0.1f);
            }
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!_isSolved)
        {
            char target = default(char);
            if (_buffer != null)
            {
                target = _generator.Current.FirstOrDefault(c => s_reverseLetterTable[c].x == (int)_buffer);
                if (target == default(char))
                    _buffer = null; // This is to correct an unsolvable state.
            }
            if (_buffer == null)
            {
                target = _generator.Current.PickRandom();
                _buttons[s_reverseLetterTable[target].x].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }

            _buttons[Array.IndexOf(layout, (ScreenColor)s_reverseLetterTable[target].y)].OnInteract();
            yield return new WaitForSeconds(0.1f);

            while (_animCount != 0)
                yield return true;
        }
    }
}
