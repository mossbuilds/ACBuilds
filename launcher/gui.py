"""ACBuilds launcher GUI (tkinter, ships with Python and the PyInstaller exe).
Step 1: pick which game client you use (original Asheron's Call, or OpenAC) and where it is installed.
Step 2: type an account, click 'Install & Play'. It installs Docker if needed, downloads and starts the server + database
containers, waits for the world to open, then starts the game."""
import argparse, ctypes, queue, sys, threading, webbrowser
import tkinter as tk
from tkinter import filedialog, messagebox, scrolledtext, ttk
from pathlib import Path

import acblauncher as L

OK, BAD = "#1a7f37", "#b42318"


class _Writer:
    def __init__(self, q):
        self.q = q

    def write(self, s):
        if s:
            self.q.put(s)

    def flush(self):
        pass


def link(parent, text, url):
    lb = tk.Label(parent, text=text, fg="#0b57d0", cursor="hand2", font=("Segoe UI", 9, "underline"))
    lb.bind("<Button-1>", lambda _e: webbrowser.open(url))
    return lb


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(f"ACBuilds Launcher {L.VERSION}")
        self.geometry("920x780")
        self.minsize(760, 620)
        self.q = queue.Queue()
        self.busy = False
        cfg = L.load_cfg()
        self.v_ctype = tk.StringVar(value=cfg.get("client_type", "retail"))
        self.v_client = tk.StringVar(value=cfg.get("client", ""))
        found = L.find_openac()
        self.v_openac = tk.StringVar(value=cfg.get("openac_dir") or (str(found[0].parent) if found else ""))
        self.v_dats = tk.StringVar(value=cfg.get("dats", ""))  # optional override
        self.v_adv = tk.BooleanVar(value=bool(cfg.get("dats")))
        self.v_account = tk.StringVar(value=cfg.get("account", ""))
        self.v_password = tk.StringVar()
        self.v_status = tk.StringVar(value="Ready.")
        self.v_retail_msg = tk.StringVar()
        self.v_openac_msg = tk.StringVar()
        self.v_acvr = tk.StringVar(value=cfg.get("acvr_path") or (str(L.find_acvr()) if L.find_acvr() else ""))
        self.v_acvr_msg = tk.StringVar()

        f = ttk.Frame(self, padding=12)
        f.pack(fill="both", expand=True)
        f.columnconfigure(0, weight=1)
        r = 0

        # ---------------- step 1: which client
        s1 = ttk.LabelFrame(f, text=" Step 1 - Which Asheron's Call client do you play with? ", padding=10)
        s1.grid(row=r, column=0, sticky="ew")
        s1.columnconfigure(0, weight=1)
        r += 1
        rb = ttk.Frame(s1)
        rb.grid(row=0, column=0, sticky="w")
        ttk.Radiobutton(rb, text="Original Asheron's Call", variable=self.v_ctype, value="retail",
                        command=self.show_panel).pack(side="left")
        ttk.Radiobutton(rb, text="OpenAC (open-source client)", variable=self.v_ctype, value="openac",
                        command=self.show_panel).pack(side="left", padx=18)
        ttk.Radiobutton(rb, text="AC:VR (PC VR, SteamVR)", variable=self.v_ctype, value="acvr",
                        command=self.show_panel).pack(side="left")

        holder = ttk.Frame(s1)
        holder.grid(row=1, column=0, sticky="ew", pady=(8, 0))
        holder.columnconfigure(0, weight=1)

        # -- original AC panel
        self.pan_retail = ttk.Frame(holder)
        self.pan_retail.grid(row=0, column=0, sticky="ew")
        self.pan_retail.columnconfigure(0, weight=1)
        ttk.Label(self.pan_retail, justify="left", wraplength=820, text=(
            "1. Install the original game client (it is not included here).\n"
            "2. Click Browse and select your acclient.exe. The DAT files in that same folder "
            "(client_portal.dat, client_cell_1.dat, ...) are used automatically.")).grid(row=0, column=0, columnspan=2, sticky="w")
        link(self.pan_retail, "How to install the original Asheron's Call client", L.AC_CLIENT_HELP).grid(
            row=1, column=0, columnspan=2, sticky="w", pady=(2, 6))
        ttk.Entry(self.pan_retail, textvariable=self.v_client).grid(row=2, column=0, sticky="ew")
        ttk.Button(self.pan_retail, text="Browse for acclient.exe...", command=self.pick_client).grid(row=2, column=1, padx=(6, 0))
        tk.Label(self.pan_retail, textvariable=self.v_retail_msg, anchor="w", justify="left", wraplength=820, font=("Segoe UI", 9)).grid(
            row=3, column=0, columnspan=2, sticky="w", pady=(4, 0))
        self.lbl_retail = self.pan_retail.grid_slaves(row=3, column=0)[0]

        # -- OpenAC panel
        self.pan_openac = ttk.Frame(holder)
        self.pan_openac.grid(row=0, column=0, sticky="ew")
        self.pan_openac.columnconfigure(0, weight=1)
        ttk.Label(self.pan_openac, justify="left", wraplength=820, text=(
            "1. Install OpenAC and run its own launcher once, so it prepares your data files.\n"
            "2. Choose the folder that contains AcDream.App.exe (filled in automatically if OpenAC is found). "
            "You still need your own game DAT files; OpenAC does not include them.")).grid(row=0, column=0, columnspan=2, sticky="w")
        link(self.pan_openac, "Get OpenAC (GitHub releases)", L.OPENAC_URL).grid(
            row=1, column=0, columnspan=2, sticky="w", pady=(2, 6))
        ttk.Entry(self.pan_openac, textvariable=self.v_openac).grid(row=2, column=0, sticky="ew")
        ttk.Button(self.pan_openac, text="Browse for OpenAC folder...", command=self.pick_openac).grid(row=2, column=1, padx=(6, 0))
        tk.Label(self.pan_openac, textvariable=self.v_openac_msg, anchor="w", justify="left", wraplength=820, font=("Segoe UI", 9)).grid(
            row=3, column=0, columnspan=2, sticky="w", pady=(4, 0))
        self.lbl_openac = self.pan_openac.grid_slaves(row=3, column=0)[0]

        # -- AC:VR panel
        self.pan_acvr = ttk.Frame(holder)
        self.pan_acvr.grid(row=0, column=0, sticky="ew")
        self.pan_acvr.columnconfigure(0, weight=1)
        ttk.Label(self.pan_acvr, justify="left", wraplength=820, text=(
            "1. Install AC:VR (Windows PC VR setup) and connect your headset (Quest: Link or Air Link). "
            "SteamVR must be your OpenXR runtime - the launcher checks and starts SteamVR for you.\n"
            "2. Choose AC-VR.bat or the 'AC VR (SteamVR)' shortcut (filled in automatically if found).\n"
            "3. Set your retail DAT folder below - the server needs it too. Account and password are entered inside AC:VR: "
            "add a custom server 127.0.0.1, port 9000, type ACE.")).grid(row=0, column=0, columnspan=2, sticky="w")
        link(self.pan_acvr, "Get AC:VR (community preview)", L.ACVR_URL).grid(row=1, column=0, columnspan=2, sticky="w", pady=(2, 6))
        ttk.Entry(self.pan_acvr, textvariable=self.v_acvr).grid(row=2, column=0, sticky="ew")
        ttk.Button(self.pan_acvr, text="Browse for AC-VR.bat...", command=self.pick_acvr).grid(row=2, column=1, padx=(6, 0))
        tk.Label(self.pan_acvr, textvariable=self.v_acvr_msg, anchor="w", justify="left", wraplength=820, font=("Segoe UI", 9)).grid(
            row=3, column=0, columnspan=2, sticky="w", pady=(4, 0))
        self.lbl_acvr = self.pan_acvr.grid_slaves(row=3, column=0)[0]

        # -- advanced: DAT folder override
        adv = ttk.Frame(s1)
        adv.grid(row=2, column=0, sticky="ew", pady=(8, 0))
        adv.columnconfigure(1, weight=1)
        ttk.Checkbutton(adv, text="Advanced: use a different DAT folder for the server", variable=self.v_adv,
                        command=self.show_panel).grid(row=0, column=0, columnspan=3, sticky="w")
        self.adv_row = ttk.Frame(adv)
        self.adv_row.grid(row=1, column=0, columnspan=3, sticky="ew")
        self.adv_row.columnconfigure(0, weight=1)
        ttk.Entry(self.adv_row, textvariable=self.v_dats).grid(row=0, column=0, sticky="ew")
        ttk.Button(self.adv_row, text="Browse...", command=self.pick_dats).grid(row=0, column=1, padx=(6, 0))

        # ---------------- step 2: account
        s2 = ttk.LabelFrame(f, text=" Step 2 - Your game account on this server ", padding=10)
        s2.grid(row=r, column=0, sticky="ew", pady=(10, 0))
        s2.columnconfigure(1, weight=1)
        r += 1
        ttk.Label(s2, text="Account (new names are created)").grid(row=0, column=0, sticky="w", pady=2)
        ttk.Entry(s2, textvariable=self.v_account).grid(row=0, column=1, sticky="ew", padx=6)
        ttk.Label(s2, text="Password").grid(row=1, column=0, sticky="w", pady=2)
        ttk.Entry(s2, textvariable=self.v_password, show="*").grid(row=1, column=1, sticky="ew", padx=6)
        self.v_acct_hint = tk.StringVar()
        ttk.Label(s2, textvariable=self.v_acct_hint, foreground="#666").grid(row=2, column=1, sticky="w", padx=6)

        # ---------------- buttons
        self.buttons = []
        rows_of_buttons = [
            [("Install & Play", self.do_install_play), ("Play only", self.do_play), ("Stop server", self.do_stop),
             ("Backup", self.do_backup), ("Update", self.do_update)],
            [("Install server only", lambda: self.do_install("server")),
             ("Install database only", lambda: self.do_install("db")),
             ("Uninstall server", lambda: self.do_uninstall("server")),
             ("Uninstall database (+backup)", lambda: self.do_uninstall("db")),
             ("Uninstall all", lambda: self.do_uninstall("all"))],
        ]
        for i, row in enumerate(rows_of_buttons):
            bar = ttk.Frame(f)
            bar.grid(row=r, column=0, pady=(10 if i == 0 else 0, 4), sticky="w")
            r += 1
            for text, fn in row:
                b = ttk.Button(bar, text=text, command=fn)
                b.pack(side="left", padx=(0, 6))
                self.buttons.append(b)

        ttk.Label(f, textvariable=self.v_status, font=("Segoe UI", 10, "bold")).grid(row=r, column=0, sticky="w")
        r += 1
        self.prog = ttk.Progressbar(f, mode="indeterminate")
        self.prog.grid(row=r, column=0, sticky="ew", pady=4)
        r += 1
        self.log = scrolledtext.ScrolledText(f, height=12, state="disabled", font=("Consolas", 9))
        self.log.grid(row=r, column=0, sticky="nsew")
        f.rowconfigure(r, weight=1)

        for v in (self.v_client, self.v_openac, self.v_ctype, self.v_dats, self.v_acvr):
            v.trace_add("write", lambda *_: self.refresh_msgs())
        self.show_panel()
        self.after(100, self.pump)

    # ---- panels and live validation
    def show_panel(self):
        kind = self.v_ctype.get()
        for name, pan in (("retail", self.pan_retail), ("openac", self.pan_openac), ("acvr", self.pan_acvr)):
            if name == kind:
                pan.grid()
            else:
                pan.grid_remove()
        self.v_acct_hint.set("Not used for AC:VR - you enter the account inside AC:VR's own login screen." if kind == "acvr" else
                             "The first account created becomes the server admin. The password is never saved.")
        if kind == "acvr":
            self.v_adv.set(True)  # AC:VR does not tell us where your DAT files are; the server needs them
        if self.v_adv.get():
            self.adv_row.grid()
        else:
            self.adv_row.grid_remove()
        self.refresh_msgs()

    def retail_dats(self):
        p = self.v_client.get().strip().strip('"')
        if p:
            d = Path(p) if Path(p).is_dir() else Path(p).parent
            if list(d.glob("client_*.dat")):
                return d
        return None

    def refresh_msgs(self):
        p = self.v_client.get().strip().strip('"')
        if not p:
            self.v_retail_msg.set("Choose your acclient.exe.")
            self.lbl_retail.configure(fg="#555")
        elif not Path(p).exists():
            self.v_retail_msg.set("Not found: that file does not exist.")
            self.lbl_retail.configure(fg=BAD)
        elif self.retail_dats():
            self.v_retail_msg.set(f"OK - client found, DAT files found in {self.retail_dats()}")
            self.lbl_retail.configure(fg=OK)
        else:
            self.v_retail_msg.set("Client found, but no client_*.dat files next to it. Tick Advanced below to choose the DAT folder.")
            self.lbl_retail.configure(fg=BAD)
        self.refresh_acvr()
        found = L.find_openac(self.v_openac.get().strip() or None)
        if found:
            self.v_openac_msg.set(f"OK - OpenAC found ({found[0].name}). DAT files: {found[1]}"
                                  + ("; prepared data package found" if found[2] else "; run OpenAC's launcher once to prepare its data"))
            self.lbl_openac.configure(fg=OK)
        else:
            self.v_openac_msg.set("OpenAC not found in that folder (looking for AcDream.App.exe)." if self.v_openac.get().strip()
                                  else "Choose the folder with AcDream.App.exe.")
            self.lbl_openac.configure(fg=BAD if self.v_openac.get().strip() else "#555")

    def refresh_acvr(self):
        f = L.find_acvr(self.v_acvr.get().strip() or None)
        rt = L.openxr_runtime()
        if f:
            extra = "" if (rt and "steam" in rt.lower()) else " - but SteamVR is not the active OpenXR runtime yet"
            self.v_acvr_msg.set(f"OK - AC:VR found ({f.name}){extra}")
            self.lbl_acvr.configure(fg=OK if not extra else BAD)
        else:
            self.v_acvr_msg.set("AC:VR not found. Install it, then Browse to AC-VR.bat or the 'AC VR (SteamVR)' shortcut."
                                if not self.v_acvr.get().strip() else "Not found: that path does not exist.")
            self.lbl_acvr.configure(fg="#555" if not self.v_acvr.get().strip() else BAD)

    def dats_dir(self):
        """Folder mounted into the server as /ace/Dats: explicit override, else derived from the chosen client."""
        if self.v_adv.get() and self.v_dats.get().strip():
            return self.v_dats.get().strip()
        if self.v_ctype.get() == "acvr":
            return None  # only an explicit DAT folder counts
        if self.v_ctype.get() == "openac":
            found = L.find_openac(self.v_openac.get().strip() or None)
            return found[1] if found else None
        d = self.retail_dats()
        return str(d) if d else None

    # ---- file pickers
    def pick_client(self):
        p = filedialog.askopenfilename(title="Select acclient.exe", filetypes=[("AC client", "acclient.exe"), ("All", "*.*")])
        if p:
            self.v_client.set(p)

    def pick_openac(self):
        p = filedialog.askdirectory(title="Folder with AcDream.App.exe (your OpenAC install)")
        if p:
            self.v_openac.set(p)

    def pick_acvr(self):
        p = filedialog.askopenfilename(title="Select AC-VR.bat (or the AC VR (SteamVR) shortcut)",
                                       filetypes=[("AC:VR launcher", "*.bat *.exe *.lnk"), ("All", "*.*")])
        if p:
            self.v_acvr.set(p)

    def pick_dats(self):
        p = filedialog.askdirectory(title="Folder with client_cell_1.dat, client_portal.dat, ...")
        if p:
            self.v_dats.set(p)

    # ---- log / status plumbing
    def pump(self):
        try:
            while True:
                s = self.q.get_nowait()
                self.log.configure(state="normal")
                self.log.insert("end", s)
                self.log.see("end")
                self.log.configure(state="disabled")
                for line in s.splitlines():
                    if line.startswith("["):
                        self.v_status.set(line)
        except queue.Empty:
            pass
        self.after(100, self.pump)

    def set_busy(self, busy):
        self.busy = busy
        for b in self.buttons:
            b.state(["disabled"] if busy else ["!disabled"])
        (self.prog.start if busy else self.prog.stop)()

    def args(self, **extra):
        a = argparse.Namespace(dats=self.dats_dir(), client=self.v_client.get().strip() or None,
                               account=self.v_account.get() or None, password=self.v_password.get() or None,
                               no_client=False, all=False, what="all", file=None,
                               client_type=self.v_ctype.get(), openac_dir=self.v_openac.get().strip() or None,
                               acvr_path=self.v_acvr.get().strip() or None)
        for k, v in extra.items():
            setattr(a, k, v)
        return a

    def remember(self):
        cfg = L.load_cfg()
        cfg.update(client=self.v_client.get().strip(), account=self.v_account.get(), client_type=self.v_ctype.get(),
                   openac_dir=self.v_openac.get().strip(), acvr_path=self.v_acvr.get().strip(),
                   dats=self.v_dats.get().strip() if self.v_adv.get() else "")
        L.save_cfg(cfg)

    def work(self, fn, done_msg):
        if self.busy:
            return
        self.remember()
        self.set_busy(True)

        def target():
            old = sys.stdout, sys.stderr
            sys.stdout = sys.stderr = _Writer(self.q)
            msg = done_msg
            try:
                fn()
            except SystemExit as e:
                if e.code not in (0, None):
                    msg = f"Stopped: {e.code}"
                    print(f"\n{e.code}")
                else:
                    msg = "Finished (see the log)."
            except Exception as e:  # never leave the window stuck
                msg = f"Error: {e}"
                print(f"\nError: {e}")
            finally:
                sys.stdout, sys.stderr = old
                self.after(0, lambda: (self.set_busy(False), self.v_status.set(msg)))

        threading.Thread(target=target, daemon=True).start()

    # ---- actions
    def need_client(self):
        if self.v_ctype.get() == "acvr":
            if not L.find_acvr(self.v_acvr.get().strip() or None):
                if messagebox.askyesno("AC:VR not found", "Could not find AC:VR. Choose AC-VR.bat or the 'AC VR (SteamVR)' "
                                       "shortcut in Step 1, or open the AC:VR download page?"):
                    webbrowser.open(L.ACVR_URL)
                return False
            if not self.v_dats.get().strip():
                messagebox.showinfo("ACBuilds", "Choose your retail DAT folder in Step 1 - the server needs your "
                                                "client_*.dat files.")
                return False
            return True
        if self.v_ctype.get() == "openac":
            if not L.find_openac(self.v_openac.get().strip() or None):
                if messagebox.askyesno("OpenAC not found",
                                       "Could not find your OpenAC install (AcDream.App.exe).\n\n"
                                       "Choose its folder in Step 1, or open the OpenAC download page?"):
                    webbrowser.open(L.OPENAC_URL)
                return False
        else:
            p = self.v_client.get().strip().strip('"')
            if not (p and Path(p).exists()):
                if messagebox.askyesno("Asheron's Call client needed",
                                       "Select your acclient.exe in Step 1 first.\n\nThe game client is not included. "
                                       "Open the install guide in your browser?"):
                    webbrowser.open(L.AC_CLIENT_HELP)
                return False
        if not self.dats_dir():
            messagebox.showinfo("ACBuilds", "Could not find your client_*.dat files. Tick 'Advanced' in Step 1 and choose "
                                            "the folder that contains them.")
            return False
        return True

    def need_account(self):
        if not (self.v_account.get() and self.v_password.get()):
            messagebox.showinfo("ACBuilds", "Enter an account name and password in Step 2 first.")
            return False
        return True

    def do_install_play(self):
        if self.need_client() and (self.v_ctype.get() == "acvr" or self.need_account()):
            self.work(lambda: L.cmd_up(self.args()), "Done. The game should be starting.")

    def do_play(self):
        if self.need_client() and (self.v_ctype.get() == "acvr" or self.need_account()):
            self.work(lambda: L.cmd_play(self.args()), "Done.")

    def do_backup(self):
        self.work(lambda: (L.ensure_docker(), L.do_backup()), "Backup finished.")

    def do_update(self):
        self.work(lambda: L.cmd_update(self.args(what="all")), "Update finished.")

    def do_stop(self):
        self.work(lambda: (L.ensure_docker(), L.compose("down")), "Server stopped.")

    def do_install(self, what):
        names = {"server": "the server only", "db": "the database only"}
        if what == "db" or self.dats_dir():
            self.work(lambda: L.cmd_install(self.args(what=what)), f"Installed {names[what]}.")
        else:
            messagebox.showinfo("ACBuilds", "Choose your game client in Step 1 first (the server needs to know where "
                                            "your DAT files are).")

    def do_uninstall(self, what):
        text = {
            "server": "Remove the ACBuilds SERVER container and image.\n\nThe database and its data are kept.",
            "db": "Remove the ACBuilds DATABASE container, image and data.\n\nA backup of accounts and characters is saved first "
                  "(acbuilds-data\\backups) and kept. The server will be stopped, because it cannot run without its database.",
            "all": "Remove the ACBuilds server AND database (containers, images, database data).\n\nA backup of accounts and "
                   "characters is saved first (acbuilds-data\\backups) and kept.",
        }[what]
        if not messagebox.askyesno("Uninstall", text + "\n\nYour Asheron's Call client and DAT files are NOT touched.\n\nContinue?",
                                   icon="warning"):
            return
        self.work(lambda: L.cmd_uninstall(self.args(what=what, yes=True, purge=False)),
                  "Uninstalled. Your AC client was not touched.")


def run_gui():
    if L.IS_WIN:
        try:  # hide the console window behind the GUI (the exe is a console build so the CLI still works)
            hwnd = ctypes.windll.kernel32.GetConsoleWindow()
            if hwnd:
                ctypes.windll.user32.ShowWindow(hwnd, 0)
        except Exception:
            pass
    App().mainloop()
