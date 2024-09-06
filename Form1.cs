using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace 吸附窗口
{
    public partial class Form1 : Form
    {
        /// <summary>
        /// 
        /// </summary>
        Process _weChatProcess;
        /// <summary>
        /// 微信聊天窗口的句柄（假设你已经找到了它）  
        /// </summary>
        private IntPtr _wechatWindowHandle;
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        /// <summary>
        /// 定时器用于定期检查窗口位置和大小
        /// </summary>        
        private System.Windows.Forms.Timer timer;
        #region 输入一段文字到焦点窗口
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        /// <summary>
        /// 
        /// </summary>
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));

            [StructLayout(LayoutKind.Explicit)]
            public struct InputUnion
            {
                [FieldOffset(0)] public MOUSEINPUT mi;
                [FieldOffset(0)] public KEYBDINPUT ki;
                [FieldOffset(0)] public HARDWAREINPUT hi;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct MOUSEINPUT
            {
                public int dx;
                public int dy;
                public uint mouseData;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct KEYBDINPUT
            {
                public ushort wVk;
                public ushort wScan;
                public uint dwFlags;
                public uint time;
                public IntPtr dwExtraInfo;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HARDWAREINPUT
            {
                public uint uMsg;
                public ushort wParamL;
                public ushort wParamH;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="vk"></param>
        /// <returns></returns>
        INPUT CreateKeyUp(ushort vk)
        {
            return new INPUT
            {
                type = 1, // INPUT_KEYBOARD  
                U = new INPUT.InputUnion
                {
                    ki = new INPUT.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = 2, // KEYEVENTF_KEYUP  
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="vk"></param>
        /// <returns></returns>
        INPUT CreateKeyDown(ushort vk)
        {
            return new INPUT
            {
                type = 1, // INPUT_KEYBOARD  
                U = new INPUT.InputUnion
                {
                    ki = new INPUT.KEYBDINPUT
                    {
                        wVk = vk,
                        wScan = 0,
                        dwFlags = 0, // 0 for key press  
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            };
        }
        #endregion
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPLACEMENT
        {
            public uint length;
            public uint flags;
            public uint showCmd;
            public POINT minPosition;
            public POINT maxPosition;
            public RECT normalPosition;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        private Task moveTask;
        private CancellationTokenSource cts;
        public Form1()
        {
            InitializeComponent();
            ShowInTaskbar = false;
        }
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            //WeChat
            //_weChatProcess = Process.GetProcessesByName("TIM")[0];
            _weChatProcess = Process.GetProcessesByName("WeChat")[0];
            _wechatWindowHandle = _weChatProcess.MainWindowHandle;
            // 初始化CancellationTokenSource  
            cts = new CancellationTokenSource();
            // 启动任务  
            moveTask = Task.Run(() => MoveWindowAsync(cts.Token), cts.Token);
            treeView1.ExpandAll();
            treeView1.NodeMouseDoubleClick += TreeView1_NodeMouseDoubleClick;
        }

        private void TreeView1_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            Clipboard.SetText(e.Node.Text);
            SetForegroundWindow(_wechatWindowHandle);
            //SimulatePaste();
            // 按下 Ctrl  
            INPUT[] inputsDown = new INPUT[1];
            inputsDown[0] = CreateKeyDown((ushort)Keys.LControlKey);
            SendInput((uint)inputsDown.Length, ref inputsDown[0], INPUT.Size);

            // 等待一段时间（可选，但有助于确保按键事件被正确处理）  
            System.Threading.Thread.Sleep(100);

            // 按下 V（粘贴）  
            // 注意：这里我们直接发送按键，而不通过SendMessage，因为SendInput更接近于物理按键  
            INPUT[] inputsV = new INPUT[1];
            inputsV[0] = CreateKeyDown((ushort)Keys.V);
            SendInput((uint)inputsV.Length, ref inputsV[0], INPUT.Size);
            System.Threading.Thread.Sleep(10); // 短暂的延时以确保按键被按下  
            inputsV[0] = CreateKeyUp((ushort)Keys.V);
            SendInput((uint)inputsV.Length, ref inputsV[0], INPUT.Size);

            // 释放 Ctrl  
            INPUT[] inputsUp = new INPUT[1];
            inputsUp[0] = CreateKeyUp((ushort)Keys.LControlKey);
            SendInput((uint)inputsUp.Length, ref inputsUp[0], INPUT.Size);

            // 发送回车  
            inputsDown[0] = CreateKeyDown((ushort)Keys.Return);
            SendInput((uint)inputsDown.Length, ref inputsDown[0], INPUT.Size);
            System.Threading.Thread.Sleep(10); // 短暂的延时  
            inputsDown[0] = CreateKeyUp((ushort)Keys.Return);
            SendInput((uint)inputsDown.Length, ref inputsDown[0], INPUT.Size);
        }
        private RECT lastRect;
        private async Task MoveWindowAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token); // 等待1秒  

                // 注意：这里需要使用Invoke或BeginInvoke来在UI线程上移动窗口  
                Invoke((MethodInvoker)delegate
                {
                    if (_wechatWindowHandle != IntPtr.Zero)
                    {
                        WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                        placement.length = (uint)Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                        if (GetWindowPlacement(_wechatWindowHandle, ref placement))
                        {
                            switch (placement.showCmd)
                            {
                                case 1: // SW_NORMAL  
                                case 3: // SW_MAXIMIZE  
                                    if (WindowState == FormWindowState.Minimized)
                                        WindowState = FormWindowState.Normal;
                                    RECT wechatRect;
                                    if (GetWindowRect(_wechatWindowHandle, out wechatRect)&& !Equals(lastRect, wechatRect))
                                    {
                                        lastRect = wechatRect;
                                        Location = new Point(wechatRect.Right - 8, wechatRect.Top);
                                        Size = new Size(Size.Width, wechatRect.Height);
                                    }
                                    break;
                                case 2: // SW_MINIMIZE  
                                    WindowState = FormWindowState.Minimized;
                                    break;
                                default:
                                    Console.WriteLine("未知窗口状态");
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("获取窗口状态失败");
                        }
                      
                    }
                });
            }
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cts.Cancel(); // 取消任务  
            moveTask.Wait(); // 等待任务完成（可选，但通常不推荐在UI线程中这样做）  

            base.OnFormClosing(e);
        }
    }
}
