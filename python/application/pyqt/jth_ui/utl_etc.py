import time


class CalcETC:
    def __init__(self, total_cycles):
        # Initialize the total number of cycles and start time
        self.total_cycles = total_cycles
        self.start_time = time.time()
        self.completed_cycles = 0

    def cycle(self):
        # Check if there are cycles left to complete
        if self.completed_cycles < self.total_cycles:
            # Increment the count of completed cycles
            self.completed_cycles += 1

            # Calculate the elapsed time since the start
            elapsed_time = time.time() - self.start_time

            # Avoid division by zero; calculate average time per cycle
            if self.completed_cycles > 0:
                average_time_per_cycle = elapsed_time / self.completed_cycles
            else:
                average_time_per_cycle = 0

            # Calculate the remaining cycles and ensure it's not negative
            remaining_cycles = max(self.total_cycles - self.completed_cycles, 0)

            # Estimate the remaining time based on the average time per cycle
            estimated_remaining_time = average_time_per_cycle * remaining_cycles

            estimated_remaining_time = max(estimated_remaining_time, 1)
        else:
            # If all cycles are completed, set the remaining time to 1 sec
            estimated_remaining_time = 1

        # Return the formatted time
        return self._format_time(estimated_remaining_time)

    def _format_time(self, seconds):
        # Convert seconds to hours, minutes, and seconds
        hours = int(seconds // 3600)
        minutes = int((seconds % 3600) // 60)
        seconds = int(seconds % 60)

        # Format the time based on whether hours are present or not
        if hours == 0:
            return f"{minutes:02d}:{seconds:02d}"
        else:
            return f"{hours:02d}:{minutes:02d}:{seconds:02d}"


def default_extension(file_path, ext):
    if not file_path.endswith(ext):
        file_path += ext
    return file_path
