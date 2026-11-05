using System.Collections.Generic;

namespace BongoKeyStats
{
    internal struct KeyCell
    {
        public int Vk; public string Label; public float X, Y, W;
        public KeyCell(int vk, string label, float x, float y, float w) { Vk = vk; Label = label; X = x; Y = y; W = w; }
    }

    internal static class KeyNames
    {
        public static readonly string[] Groups = { "Letters", "Numbers", "Modifiers", "Function", "Navigation", "Symbols", "Numpad", "Other" };

        private static readonly string[] Names = new string[256];
        public static readonly List<KeyCell> Layout = new List<KeyCell>();

        static KeyNames()
        {
            for (int i = 0; i < 256; i++) Names[i] = "VK" + i.ToString("X2");
            for (int c = 'A'; c <= 'Z'; c++) Names[c] = ((char)c).ToString();
            for (int c = '0'; c <= '9'; c++) Names[c] = ((char)c).ToString();
            for (int i = 0; i < 10; i++) Names[0x60 + i] = "Num" + i;
            for (int i = 1; i <= 24; i++) Names[0x6F + i] = "F" + i;
            Set(0x08, "Backspace"); Set(0x09, "Tab"); Set(0x0C, "Clear"); Set(0x0D, "Enter"); Set(0x13, "Pause");
            Set(0x14, "CapsLock"); Set(0x1B, "Esc"); Set(0x20, "Space"); Set(0x21, "PgUp"); Set(0x22, "PgDn");
            Set(0x23, "End"); Set(0x24, "Home"); Set(0x25, "Left"); Set(0x26, "Up"); Set(0x27, "Right"); Set(0x28, "Down");
            Set(0x2C, "PrtSc"); Set(0x2D, "Insert"); Set(0x2E, "Delete"); Set(0x5B, "LWin"); Set(0x5C, "RWin"); Set(0x5D, "Menu");
            Set(0x6A, "Num*"); Set(0x6B, "Num+"); Set(0x6C, "NumSep"); Set(0x6D, "Num-"); Set(0x6E, "Num."); Set(0x6F, "Num/");
            Set(0x90, "NumLock"); Set(0x91, "ScrollLock");
            Set(0xA0, "LShift"); Set(0xA1, "RShift"); Set(0xA2, "LCtrl"); Set(0xA3, "RCtrl"); Set(0xA4, "LAlt"); Set(0xA5, "RAlt");
            Set(0xBA, ";"); Set(0xBB, "="); Set(0xBC, ","); Set(0xBD, "-"); Set(0xBE, "."); Set(0xBF, "/"); Set(0xC0, "`");
            Set(0xDB, "["); Set(0xDC, "\\"); Set(0xDD, "]"); Set(0xDE, "'"); Set(0xE2, "\\ (ISO)");

            Add(0x1B, "Esc", 0, 0, 1f);
            float fx = 2f;
            for (int i = 1; i <= 12; i++)
            {
                if (i == 5 || i == 9) fx += 0.5f;
                Add(0x6F + i, "F" + i, fx, 0, 1f); fx += 1f;
            }
            Add(0x2C, "PrtSc", 15f, 0, 1f);
            Add(0x91, "ScrLk", 16f, 0, 1f);
            Add(0x13, "Pause", 17f, 0, 1f);

            float x = 0;
            int[] r1 = { 0xC0, '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', 0xBD, 0xBB };
            foreach (int v in r1) { Add(v, Names[v], x, 1.5f, 1f); x += 1f; }
            Add(0x08, "Bksp", x, 1.5f, 2f);
            Add(0x09, "Tab", 0, 2.5f, 1.5f); x = 1.5f;
            int[] r2 = { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P', 0xDB, 0xDD };
            foreach (int v in r2) { Add(v, Names[v], x, 2.5f, 1f); x += 1f; }
            Add(0xDC, "\\", x, 2.5f, 1.5f);
            Add(0x14, "Caps", 0, 3.5f, 1.75f); x = 1.75f;
            int[] r3 = { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', 0xBA, 0xDE };
            foreach (int v in r3) { Add(v, Names[v], x, 3.5f, 1f); x += 1f; }
            Add(0x0D, "Enter", x, 3.5f, 2.25f);
            Add(0xA0, "LShift", 0, 4.5f, 2.25f); x = 2.25f;
            int[] r4 = { 'Z', 'X', 'C', 'V', 'B', 'N', 'M', 0xBC, 0xBE, 0xBF };
            foreach (int v in r4) { Add(v, Names[v], x, 4.5f, 1f); x += 1f; }
            Add(0xA1, "RShift", x, 4.5f, 2.75f);
            Add(0xA2, "LCtrl", 0, 5.5f, 1.25f);
            Add(0x5B, "Win", 1.25f, 5.5f, 1.25f);
            Add(0xA4, "LAlt", 2.5f, 5.5f, 1.25f);
            Add(0x20, "Space", 3.75f, 5.5f, 6.25f);
            Add(0xA5, "RAlt", 10f, 5.5f, 1.25f);
            Add(0x5C, "RWin", 11.25f, 5.5f, 1.25f);
            Add(0x5D, "Menu", 12.5f, 5.5f, 1.25f);
            Add(0xA3, "RCtrl", 13.75f, 5.5f, 1.25f);
            Add(0x2D, "Ins", 15f, 1.5f, 1f); Add(0x24, "Home", 16f, 1.5f, 1f); Add(0x21, "PgUp", 17f, 1.5f, 1f);
            Add(0x2E, "Del", 15f, 2.5f, 1f); Add(0x23, "End", 16f, 2.5f, 1f); Add(0x22, "PgDn", 17f, 2.5f, 1f);
            Add(0x26, "Up", 16f, 4.5f, 1f);
            Add(0x25, "Left", 15f, 5.5f, 1f); Add(0x28, "Down", 16f, 5.5f, 1f); Add(0x27, "Right", 17f, 5.5f, 1f);
            float nx = 18.5f;
            Add(0x90, "Num", nx, 1.5f, 1f); Add(0x6F, "/", nx + 1, 1.5f, 1f); Add(0x6A, "*", nx + 2, 1.5f, 1f); Add(0x6D, "-", nx + 3, 1.5f, 1f);
            Add(0x67, "7", nx, 2.5f, 1f); Add(0x68, "8", nx + 1, 2.5f, 1f); Add(0x69, "9", nx + 2, 2.5f, 1f); Add(0x6B, "+", nx + 3, 2.5f, 1f);
            Add(0x64, "4", nx, 3.5f, 1f); Add(0x65, "5", nx + 1, 3.5f, 1f); Add(0x66, "6", nx + 2, 3.5f, 1f);
            Add(0x61, "1", nx, 4.5f, 1f); Add(0x62, "2", nx + 1, 4.5f, 1f); Add(0x63, "3", nx + 2, 4.5f, 1f);
            Add(0x60, "0", nx, 5.5f, 2f); Add(0x6E, ".", nx + 2, 5.5f, 1f);
        }

        private static void Add(int vk, string label, float x, float y, float w) { Layout.Add(new KeyCell(vk, label, x, y, w)); }
        private static void Set(int vk, string n) { Names[vk] = n; }

        public static string Name(int vk) { return vk >= 0 && vk < 256 ? Names[vk] : "VK" + vk; }

        public static int Group(int vk)
        {
            if (vk >= 'A' && vk <= 'Z') return 0;
            if (vk >= '0' && vk <= '9') return 1;
            if ((vk >= 0xA0 && vk <= 0xA5) || vk == 0x5B || vk == 0x5C || vk == 0x5D || (vk >= 0x10 && vk <= 0x12)) return 2;
            if (vk >= 0x70 && vk <= 0x87) return 3;
            if ((vk >= 0x21 && vk <= 0x28) || vk == 0x2D || vk == 0x2E) return 4;
            if ((vk >= 0xBA && vk <= 0xC0) || (vk >= 0xDB && vk <= 0xDF) || vk == 0xE2) return 5;
            if ((vk >= 0x60 && vk <= 0x6F) || vk == 0x90) return 6;
            return 7;
        }
    }
}
