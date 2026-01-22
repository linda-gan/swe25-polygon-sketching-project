import tkinter as tk

class DrawingApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Baseline: Polygon Sketching")
        
        # --- STATE (Mutable) ---
        self.polygons = []       # List of finished shapes (lists of points)
        self.current_poly = []   # Current shape being drawn
        self.mouse_pos = (0, 0)  # Current mouse position
        
        # Undo/Redo Stacks
        self.undo_stack = []
        self.redo_stack = []

        # --- UI SETUP ---
        self.canvas = tk.Canvas(root, width=800, height=600, bg="white")
        self.canvas.pack()
        
        controls = tk.Frame(root)
        controls.pack()
        tk.Button(controls, text="Undo", command=self.undo).pack(side=tk.LEFT)
        tk.Button(controls, text="Redo", command=self.redo).pack(side=tk.LEFT)
        tk.Button(controls, text="Clear all", command=self.clearall).pack(side=tk.LEFT)

        # --- EVENTS ---
        self.canvas.bind("<Button-1>", self.on_click)        # Left Click
        self.canvas.bind("<Double-Button-1>", self.on_finish)# Double Click
        self.canvas.bind("<Motion>", self.on_move)           # Mouse Move

        #self.save_state() # Initial state

    # --- LOGIC (Imperative) ---
    
    def clearall(self):
        self.save_state() # Save before modifying
        self.canvas.delete("all")
        self.polygons = []
        self.current_poly = []
        self.redraw()

    def save_state(self):
        """Snapshots current state for Undo"""
        # We must clone the lists to save a snapshot, otherwise python references the same object
        state_snapshot = {
            'polygons': [p[:] for p in self.polygons],
            'current_poly': self.current_poly[:]
        }
        self.undo_stack.append(state_snapshot)
        self.redo_stack.clear() # New action clears redo history

    def restore_state(self, state):
        """Overwrites current state with a snapshot"""
        self.polygons = [p[:] for p in state['polygons']]
        self.current_poly = state['current_poly'][:]
        self.redraw()

    def undo(self):
        if len(self.undo_stack) > 0:
            # 1. Snapshot the CURRENT live state to Redo (so we can come back)
            current_state = {
                'polygons': [p[:] for p in self.polygons],
                'current_poly': self.current_poly[:]
            }
            self.redo_stack.append(current_state)

            # 2. populate the last saved state from Undo and restore it
            previous_state = self.undo_stack.pop()
            self.restore_state(previous_state)

    def redo(self):
        if self.redo_stack:
            # 1. save the CURRENT live state to Undo
            current_state = {
                'polygons': [p[:] for p in self.polygons], # : list clicing to copy
                'current_poly': self.current_poly[:] 
            }
            self.undo_stack.append(current_state)

            # 2. populate the future state from Redo and restore it
            next_state = self.redo_stack.pop()
            self.restore_state(next_state)

    def on_click(self, event):
        self.save_state() # Save before modifying
        self.current_poly.append((event.x, event.y))
        self.redraw()

    def on_move(self, event):
        self.mouse_pos = (event.x, event.y)
        self.redraw()

    def on_finish(self, event):
        if len(self.current_poly) > 2:
            self.save_state()
            self.polygons.append(self.current_poly)
            self.current_poly = []
            self.redraw()

    def redraw(self):
        self.canvas.delete("all")
        
        # Draw finished shapes (Now as open lines, not filled polygons)
        for poly in self.polygons:
            if len(poly) > 1:
                # This prevents auto-closing the shape and prevents filling it with color
                self.canvas.create_line(poly, fill="black", width=2)
        
        # Draw current shape under construction
        if len(self.current_poly) > 0:
            # Draw lines between existing points
            if len(self.current_poly) > 1:
                self.canvas.create_line(self.current_poly, fill="red", width=2)
            
            # Draw rubber-band line (Preview)
            last_point = self.current_poly[-1]
            self.canvas.create_line(last_point, self.mouse_pos, fill="gray", dash=(4, 2))

if __name__ == "__main__":
    root = tk.Tk()
    app = DrawingApp(root)
    root.mainloop()