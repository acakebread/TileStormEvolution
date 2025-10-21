import tkinter as tk
from tkinter import ttk, filedialog, messagebox, simpledialog
import git
from datetime import datetime, timedelta
from collections import defaultdict
import matplotlib.pyplot as plt
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg, NavigationToolbar2Tk
import pandas as pd
import numpy as np
from matplotlib.colors import ListedColormap, BoundaryNorm
import os
import sys

class GitGraphApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Git Contribution Graph")
        self.repo_path = None
        self.activity = {}
        self.start_date = datetime.now().date() - timedelta(days=365)  # Default 365 days ago
        self.end_date = datetime.now().date()  # October 21, 2025, 6:44 PM BST
        self.email = None
        
        self.setup_ui()
        self.load_repo()

    def setup_ui(self):
        # Main frame to handle resizing
        self.main_frame = ttk.Frame(self.root)
        self.main_frame.pack(fill=tk.BOTH, expand=True)
        
        # Control frame
        self.control_frame = ttk.Frame(self.main_frame)
        self.control_frame.pack(fill=tk.X, pady=5)
        
        # Settings button
        self.settings_btn = ttk.Button(self.control_frame, text="Settings", command=self.open_settings)
        self.settings_btn.pack(side=tk.LEFT, padx=5)
        
        # Canvas frame for plot
        self.canvas_frame = ttk.Frame(self.main_frame)
        self.canvas_frame.pack(fill=tk.BOTH, expand=True)
        
        # Plot setup
        self.fig, (self.ax_bar, self.ax_heat) = plt.subplots(2, 1, figsize=(12, 6), 
                                                            gridspec_kw={'height_ratios': [1, 3]})
        self.canvas = FigureCanvasTkAgg(self.fig, master=self.canvas_frame)
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)
        self.toolbar = NavigationToolbar2Tk(self.canvas, self.canvas_frame)
        self.toolbar.update()
        self.canvas._tkcanvas.pack(fill=tk.BOTH, expand=True)

    def load_repo(self):
        current_dir = os.getcwd()
        try:
            git.Repo(current_dir)
            self.repo_path = current_dir
        except git.InvalidGitRepositoryError:
            self.repo_path = filedialog.askdirectory(title="Select Git Repository Folder")
        
        if self.repo_path:
            self.update_graph()
        else:
            messagebox.showerror("Error", "No valid Git repository selected.")
            self.root.quit()

    def get_commit_activity(self, email=None, start_date_str=None, end_date_str=None, max_days=365):
        try:
            repo = git.Repo(self.repo_path)
            start_date = datetime.strptime(start_date_str, '%Y-%m-%d').date() if start_date_str else None
            end_date = datetime.strptime(end_date_str, '%Y-%m-%d').date() if end_date_str else self.end_date
            
            commits = repo.iter_commits(author=email) if email else repo.iter_commits()
            
            activity = defaultdict(int)
            oldest_date = None
            for commit in commits:
                date = datetime.fromtimestamp(commit.committed_date).date()
                if date > end_date:
                    continue
                activity[date] += 1
                if oldest_date is None or date < oldest_date:
                    oldest_date = date
            
            if not activity:
                raise ValueError("No commits found in the repository (or matching the filter).")
            
            if start_date:
                calc_start = start_date
            else:
                if oldest_date:
                    repo_age_days = (end_date - oldest_date).days
                    calc_start = end_date - timedelta(days=max_days) if repo_age_days > max_days else oldest_date
                else:
                    calc_start = end_date - timedelta(days=max_days)
            
            calc_start = max(calc_start, oldest_date) if oldest_date else calc_start
            filtered_activity = {d: c for d, c in activity.items() if calc_start <= d <= end_date}
            return filtered_activity, calc_start
        except git.InvalidGitRepositoryError:
            raise ValueError("Selected folder is not a valid Git repository.")
        except ValueError as e:
            raise ValueError(f"Invalid date or data: {str(e)}")
        except Exception as e:
            raise ValueError(f"Error in commit activity: {str(e)}")

    def update_graph(self):
        try:
            # Reinitialize figure and axes to reset layout
            plt.close(self.fig)  # Close the old figure
            self.fig, (self.ax_bar, self.ax_heat) = plt.subplots(2, 1, figsize=(12, 6), 
                                                               gridspec_kw={'height_ratios': [1, 3]})
            self.canvas.figure = self.fig
            self.canvas.draw()
            
            self.activity, self.start_date = self.get_commit_activity(self.email, 
                                                                   str(self.start_date)[:10], 
                                                                   str(self.end_date)[:10])
            if not self.activity:
                messagebox.showwarning("Warning", "No commit data available for the selected range.")
                return
            
            repo_name = os.path.basename(self.repo_path.rstrip(os.sep))
            
            all_dates = pd.date_range(start=self.start_date, end=self.end_date)
            first_day_weekday = self.start_date.weekday()
            offset = (first_day_weekday + 1) % 7
            counts = [0] * offset + [self.activity.get(d.date(), 0) for d in all_dates]
            
            total_days = len(counts)
            num_weeks = (total_days + 6) // 7
            total_cells = num_weeks * 7
            pad_end = total_cells - total_days
            counts += [0] * pad_end
            
            data = np.array(counts).reshape(num_weeks, 7).T
            weekly_totals = data.sum(axis=0)
            
            colors = ['#ebedf0', '#9be9a8', '#40c463', '#30a14e', '#216e39']
            cmap = ListedColormap(colors)
            bounds = [0, 1, 3, 7, 12, np.max(data) + 1]
            norm = BoundaryNorm(bounds, cmap.N)
            midpoints = [(bounds[i] + bounds[i+1]) / 2 for i in range(len(bounds)-1)]
            
            self.ax_bar.bar(range(num_weeks), weekly_totals, color='skyblue', edgecolor='black', width=1.0)
            self.ax_bar.set_title(f'Weekly Commit Totals - {repo_name}')
            self.ax_bar.set_ylabel('Commits')
            self.ax_bar.grid(axis='y', linestyle='--', alpha=0.7)
            self.ax_bar.set_xlim(-0.5, num_weeks - 0.5)
            
            im = self.ax_heat.imshow(data, cmap=cmap, norm=norm, aspect='auto')
            self.ax_heat.set_yticks(range(7))
            self.ax_heat.set_yticklabels(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'])
            self.ax_heat.yaxis.tick_right()
            self.ax_heat.set_xlim(-0.5, num_weeks - 0.5)
            
            tick_positions = [0]
            month_labels = [self.start_date.strftime('%b')]
            current_month = self.start_date.month
            col = 0
            for i in range(offset, total_days, 7):
                week_start = self.start_date + timedelta(days=(i - offset))
                if week_start.month != current_month:
                    tick_positions.append(col)
                    month_labels.append(week_start.strftime('%b'))
                    current_month = week_start.month
                col += 1
            
            self.ax_heat.set_xticks(tick_positions)
            self.ax_heat.set_xticklabels(month_labels)
            self.ax_bar.set_xticks(tick_positions)
            self.ax_bar.set_xticklabels(month_labels, rotation=45, fontsize=8)
            
            self.fig.subplots_adjust(bottom=0.15, top=0.9)  # Control margins
            cbar = plt.colorbar(im, ax=self.ax_heat, orientation='horizontal', pad=0.1, ticks=midpoints)
            cbar.set_ticklabels(['No commits', '1-2', '3-6', '7-11', '12+'])
            
            self.root.title(f'Git Contribution Graph - {repo_name}')
            self.fig.suptitle(f'Git Contribution Calendar - {repo_name}')
            
            self.canvas.draw()
            self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)
        except ValueError as e:
            messagebox.showerror("Error", f"Error updating graph: {str(e)}")
        except Exception as e:
            messagebox.showerror("Error", f"Unexpected error updating graph: {str(e)}")

    def open_settings(self):
        settings_window = tk.Toplevel(self.root)
        settings_window.title("Settings")
        settings_window.geometry("300x250")
        
        ttk.Label(settings_window, text="Email (leave blank for all):").pack(pady=5)
        email_entry = ttk.Entry(settings_window)
        email_entry.insert(0, self.email or "")
        email_entry.pack(pady=5)
        
        ttk.Label(settings_window, text="Start Date (YYYY-MM-DD, blank for 365 days ago):").pack(pady=5)
        start_date_entry = ttk.Entry(settings_window)
        start_date_entry.insert(0, str(self.start_date)[:10])
        start_date_entry.pack(pady=5)
        
        ttk.Label(settings_window, text="End Date (YYYY-MM-DD, blank for today):").pack(pady=5)
        end_date_entry = ttk.Entry(settings_window)
        end_date_entry.insert(0, str(self.end_date)[:10])
        end_date_entry.pack(pady=5)
        
        def save_settings():
            self.email = email_entry.get() if email_entry.get() else None
            start_date = start_date_entry.get() if start_date_entry.get() else None
            end_date = end_date_entry.get() if end_date_entry.get() else None
            if start_date:
                try:
                    self.start_date = datetime.strptime(start_date, '%Y-%m-%d').date()
                except ValueError:
                    messagebox.showerror("Error", "Invalid start date format. Use YYYY-MM-DD.")
                    return
            if end_date:
                try:
                    self.end_date = datetime.strptime(end_date, '%Y-%m-%d').date()
                except ValueError:
                    messagebox.showerror("Error", "Invalid end date format. Use YYYY-MM-DD.")
                    return
            if self.start_date > self.end_date:
                messagebox.showerror("Error", "Start date cannot be after end date.")
                return
            settings_window.destroy()
            self.update_graph()

        ttk.Button(settings_window, text="Save", command=save_settings).pack(pady=10)

    def on_closing(self):
        self.root.quit()

def main():
    try:
        root = tk.Tk()
        app = GitGraphApp(root)
        root.protocol("WM_DELETE_WINDOW", app.on_closing)
        root.mainloop()
    except Exception as e:
        messagebox.showerror("Error", f"Unexpected error: {str(e)}")
        if not getattr(sys, 'frozen', False):
            print(f"Error: {str(e)}")
            input("Press Enter to exit...")

if __name__ == '__main__':
    if getattr(sys, 'frozen', False):
        import multiprocessing
        multiprocessing.freeze_support()
    main()