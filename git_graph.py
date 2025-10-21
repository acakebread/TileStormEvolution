import tkinter as tk
from tkinter import filedialog, messagebox
import git
from datetime import datetime, timedelta
from collections import defaultdict
import matplotlib.pyplot as plt
import pandas as pd
import numpy as np
from matplotlib.colors import ListedColormap, BoundaryNorm
import os
import sys

def get_commit_activity(repo_path, max_days=365):
    try:
        repo = git.Repo(repo_path)
        commits = repo.iter_commits()
        activity = defaultdict(int)
        oldest_date = None
        for commit in commits:
            date = datetime.fromtimestamp(commit.committed_date).date()
            activity[date] += 1
            if oldest_date is None or date < oldest_date:
                oldest_date = date
        if not activity:
            raise ValueError("No commits found in the repository.")
        # Determine start date: max_days or oldest if repo is younger
        if oldest_date:
            repo_age_days = (datetime.now().date() - oldest_date).days
            start_date = (datetime.now().date() - timedelta(days=max_days) 
                         if repo_age_days > max_days else oldest_date)
        else:
            start_date = datetime.now().date() - timedelta(days=max_days)
        # Filter activity to start_date onwards
        filtered_activity = {d: c for d, c in activity.items() if d >= start_date}
        return filtered_activity, start_date
    except git.InvalidGitRepositoryError:
        raise ValueError("Selected folder is not a valid Git repository.")
    except Exception as e:
        raise ValueError(f"Error reading repo: {str(e)}")

def plot_calendar(activity, start_date, repo_name):
    try:
        end_date = datetime.now().date()
        all_dates = pd.date_range(start=start_date, end=end_date)
        
        first_day_weekday = start_date.weekday()  # 0=Mon, 6=Sun
        offset = (first_day_weekday + 1) % 7  # Adjust for Sun start
        counts = [0] * offset + [activity.get(d.date(), 0) for d in all_dates]
        
        total_days = len(counts)
        num_weeks = (total_days + 6) // 7
        total_cells = num_weeks * 7
        pad_end = total_cells - total_days
        counts += [0] * pad_end
        
        data = np.array(counts).reshape(num_weeks, 7).T  # Shape: (7, num_weeks)
        
        # Weekly totals for bar chart (full span including partial week)
        weekly_totals = data.sum(axis=0)  # Sum per week, including all weeks
        
        # GitHub-like colors and levels
        colors = ['#ebedf0', '#9be9a8', '#40c463', '#30a14e', '#216e39']
        cmap = ListedColormap(colors)
        bounds = [0, 1, 3, 7, 12, np.max(data) + 1]
        norm = BoundaryNorm(bounds, cmap.N)
        
        # Compute midpoints for colorbar ticks
        midpoints = [(bounds[i] + bounds[i+1]) / 2 for i in range(len(bounds)-1)]
        
        # Two subplots: top bar, bottom heatmap
        fig, (ax_bar, ax_heat) = plt.subplots(2, 1, figsize=(12, 6), gridspec_kw={'height_ratios': [1, 3]})
        fig.subplots_adjust(left=0.05, right=0.9, top=0.85, bottom=0.05, hspace=0.25)
        
        # Bar chart (weekly totals)
        bar_positions = range(num_weeks)
        ax_bar.bar(bar_positions, weekly_totals, color='skyblue', edgecolor='black', width=1.0)
        ax_bar.set_title(f'Weekly Commit Totals - {repo_name}')
        ax_bar.set_ylabel('Commits')
        ax_bar.grid(axis='y', linestyle='--', alpha=0.7)
        ax_bar.set_xlim(-0.5, num_weeks - 0.5)
        
        # Heatmap
        im = ax_heat.imshow(data, cmap=cmap, norm=norm, aspect='auto')
        
        # Y-axis: Days of week (Sun at top)
        ax_heat.set_yticks(range(7))
        ax_heat.set_yticklabels(['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'])
        ax_heat.yaxis.tick_right()
        ax_heat.set_xlim(-0.5, num_weeks - 0.5)
        
        # X-axis: Month labels at start of each month for both plots
        tick_positions = [0]
        month_labels = [start_date.strftime('%b')]
        current_month = start_date.month
        col = 0
        for i in range(offset, total_days, 7):
            week_start = start_date + timedelta(days=(i - offset))
            if week_start.month != current_month:
                tick_positions.append(col)
                month_labels.append(week_start.strftime('%b'))
                current_month = week_start.month
            col += 1
        
        ax_heat.set_xticks(tick_positions)
        ax_heat.set_xticklabels(month_labels)
        
        ax_bar.set_xticks(tick_positions)
        ax_bar.set_xticklabels(month_labels, rotation=45, fontsize=8)
        
        # Colorbar below heatmap
        cbar = fig.colorbar(im, ax=ax_heat, orientation='horizontal', pad=0.2, ticks=midpoints)
        cbar.set_ticklabels(['No commits', '1-2', '3-6', '7-11', '12+'])
        
        # Set window title and main title
        fig.canvas.manager.set_window_title(f'Git Contribution Graph - {repo_name}')
        plt.suptitle(f'Git Contribution Calendar - {repo_name}')
        
        # Disable panning/zooming to prevent dragging outside
        plt.get_current_fig_manager().toolbar.pan = lambda: None
        plt.get_current_fig_manager().toolbar.zoom = lambda: None
        
        plt.show()
    except Exception as e:
        messagebox.showerror("Error", f"Unexpected error: {str(e)}")
        if not getattr(sys, 'frozen', False):
            print(f"Error: {str(e)}")
            input("Press Enter to exit...")

def main():
    try:
        # Try current directory first
        current_dir = os.getcwd()
        repo_path = None
        try:
            git.Repo(current_dir)
            repo_path = current_dir
        except git.InvalidGitRepositoryError:
            pass
        
        if repo_path:
            try:
                activity, start_date = get_commit_activity(repo_path)
                repo_name = os.path.basename(repo_path.rstrip(os.sep))
                plot_calendar(activity, start_date, repo_name)
            except ValueError as e:
                messagebox.showerror("Error", str(e))
                repo_path = None
        
        # If no valid repo path, prompt for one
        if not repo_path:
            root = tk.Tk()
            root.withdraw()  # Hide main window
            repo_path = filedialog.askdirectory(title="Select Git Repository Folder")
            if repo_path:
                try:
                    activity, start_date = get_commit_activity(repo_path)
                    repo_name = os.path.basename(repo_path.rstrip(os.sep))
                    plot_calendar(activity, start_date, repo_name)
                except ValueError as e:
                    messagebox.showerror("Error", str(e))
            root.destroy()
    except Exception as e:
        messagebox.showerror("Error", f"Unexpected error: {str(e)}")
        if not getattr(sys, 'frozen', False):
            print(f"Error: {str(e)}")
            input("Press Enter to exit...")

if __name__ == '__main__':
    # Hide console if frozen to exe
    if getattr(sys, 'frozen', False):
        import multiprocessing
        multiprocessing.freeze_support()
    main()