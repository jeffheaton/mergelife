import os
import const_values
import datetime
import glob
import logging

# Create the directory if it doesn't exist
if not os.path.exists(const_values.LOG_DIR):
    os.makedirs(const_values.LOG_DIR)

    # Create the directory if it doesn't exist
if not os.path.exists(const_values.SETTING_DIR):
    os.makedirs(const_values.SETTING_DIR)

# Define a function to handle deletion of old log files
def delete_old_logs():
    retention_period = 7  # days
    current_time = datetime.datetime.now()
    log_files = glob.glob(os.path.join(const_values.LOG_DIR, '*.log'))

    for file in log_files:
        creation_time = datetime.datetime.fromtimestamp(os.path.getctime(file))
        if (current_time - creation_time).days > retention_period:
            os.remove(file)


# Define the logging configuration
def setup_logging():
    os.makedirs(const_values.LOG_DIR, exist_ok=True)
    
    # Get the current date to append to the log filename
    date_str = datetime.datetime.now().strftime('%Y-%m-%d')
    log_filename = os.path.join(const_values.LOG_DIR, f"{date_str}.log")

    # Set up logging
    logger = logging.getLogger()
    logger.setLevel(logging.DEBUG)

    # Create a file handler to write log messages to the file
    file_handler = logging.handlers.TimedRotatingFileHandler(log_filename, when="midnight", interval=1, backupCount=7)
    file_handler.setLevel(logging.DEBUG)
    
    # Create a formatter to format the log messages
    formatter = logging.Formatter('%(asctime)s - %(levelname)s - %(message)s')
    file_handler.setFormatter(formatter)

    # Add the handler to the logger
    logger.addHandler(file_handler)

    # Add a stream handler (console handler) for console output
    console_handler = logging.StreamHandler()
    console_handler.setLevel(logging.INFO)
    console_handler.setFormatter(formatter)
    logger.addHandler(console_handler)
