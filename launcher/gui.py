"""ACBuilds launcher GUI (tkinter, ships with Python and the PyInstaller exe).
One window: pick your acclient.exe, type an account, click 'Install & Play'. It installs Docker if needed, downloads and
starts the server + database containers, waits for the world to open, then starts the game."""
import argparse, ctypes, queue, sys, threading
import tkinter as tk
from tkinter import filedialog, messagebox, scrolledtext, ttk
from pathlib import Path

import acblauncher as L


class _Writer:
    def __init__(self, q):
        self.q = q

    def write(self, s):
        if s:
            self.q.put(s)

    def flush(self):
        pass


class App(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title(f"ACBuilds Launcher {L.VERSION}")
        self.geometry("900x640")
        self.minsize(640, 480)
        self.q = queue.Queue()
        self.busy = False
        cfg = L.load_cfg()
        self.v_client = tk.StringVar(value=cfg.get("client", ""))
        self.v_dats = tk.StringVar(value=cfg.get("dats", ""))
        self.v_account = tk.StringVar(value=cfg.get("account", ""))
        self.v_password = tk.StringVar()
        self.v_status = tk.StringVar(value="Ready.")

        f = ttk.Frame(self, padding=12)
        f.pack(fill="both", expand=True)
        f.columnconfigure(1, weight=1)
        rows = [("Asheron's Call (acclient.exe)", self.v_client, self.pick_client),
                ("AC DAT files folder", self.v_dats, self.pick_dats)]
        for i, (label, var, cmd) in enumerate(rows):
            ttk.Label(f, text=label).grid(row=i, column=0, sticky="w", pady=3)
            ttk.Entry(f, textvariable=var).grid(row=i, column=1, sticky="ew", padx=6)
            ttk.Button(f, text="Browse...", command=cmd).grid(row=i, column=2)
        ttk.Label(f, text="Account (new names are created)").grid(row=2, column=0, sticky="w", pady=3)
        ttk.Entry(f, textvariable=self.v_account).grid(row=2, column=1, sticky="ew", padx=6)
        ttk.Label(f, text="Password").grid(row=3, column=0, sticky="w", pady=3)
        ttk.Entry(f, textvariable=self.v_password, show="*").grid(row=3, column=1, sticky="ew", padx=6)
        ttk.Label(f, text="The first account created becomes the server admin.", foreground="#666").grid(
            row=4, column=1, sticky="w", padx=6)

        self.buttons = []
        rows_of_buttons = [
            [("Install & Play", self.do_install_play), ("Play only", self.do_play), ("Stop server", self.do_stop),
             ("Backup", self.do_backup), ("Update", self.do_update)],
            [("Install server only", lambda: self.do_install("server")), ("Install database only", lambda: self.do_install("db")),
             ("Uninstall server", lambda: self.do_uninstall("server")),
             ("Uninstall database (+backup)", lambda: self.do_uninstall("db")),
             ("Uninstall all", lambda: self.do_uninstall("all"))],
        ]
        for r, row in enumerate(rows_of_buttons):
            bar = ttk.Frame(f)
            bar.grid(row=5 + r, column=0, columnspan=3, pady=(10 if r == 0 else 0, 4), sticky="w")
            for text, fn in row:
                b = ttk.Button(bar, text=text, command=fn)
                b.pack(side="left", padx=(0, 6))
                self.buttons.append(b)

        ttk.Label(f, textvariable=self.v_status, font=("Segoe UI", 10, "bold")).grid(
            row=7, column=0, columnspan=3, sticky="w")
        self.prog = ttk.Progressbar(f, mode="indeterminate")
        self.prog.grid(row=8, column=0, columnspan=3, sticky="ew", pady=4)
        self.log = scrolledtext.ScrolledText(f, height=18, state="disabled", font=("Consolas", 9))
        self.log.grid(row=9, column=0, columnspan=3, sticky="nsew")
        f.rowconfigure(9, weight=1)
        self.after(100, self.pump)

    # ---- file pickers
    def pick_client(self):
        p = filedialog.askopenfilename(title="Select acclient.exe", filetypes=[("AC client", "acclient.exe"), ("All", "*.*")])
        if p:
            self.v_client.set(p)
            if not self.v_dats.get() and list(Path(p).parent.glob("client_*.dat")):
                self.v_dats.set(str(Path(p).parent))  # the retail install folder already holds the DAT files

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
        a = argparse.Namespace(dats=self.v_dats.get() or None, client=self.v_client.get() or None,
                               account=self.v_account.get() or None, password=self.v_password.get() or None,
                               no_client=False, all=False, what="all", file=None)
        for k, v in extra.items():
            setattr(a, k, v)
        return a

    def remember(self):
        cfg = L.load_cfg()
        cfg.update(client=self.v_client.get(), dats=self.v_dats.get(), account=self.v_account.get())
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
    def need_account(self):
        if not (self.v_account.get() and self.v_password.get()):
            messagebox.showinfo("ACBuilds", "Enter an account name and password first.")
            return False
        return True

    def do_install_play(self):
        if self.need_account():
            self.work(lambda: L.cmd_up(self.args()), "Done. The game should be starting.")

    def do_play(self):
        if self.need_account():
            self.work(lambda: L.cmd_play(self.args()), "Done.")

    def do_backup(self):
        self.work(lambda: (L.ensure_docker(), L.do_backup()), "Backup finished.")

    def do_update(self):
        self.work(lambda: L.cmd_update(self.args(what="all")), "Update finished.")

    def do_stop(self):
        self.work(lambda: (L.ensure_docker(), L.compose("down")), "Server stopped.")


    def do_install(self, what):
        names = {"server": "the server only", "db": "the database only"}
        self.work(lambda: L.cmd_install(self.args(what=what)), f"Installed {names[what]}.")

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
