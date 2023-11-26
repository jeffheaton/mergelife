import logging
import logging.config
import logging.handlers

from heaton_ca_app import AppHeatonCA

logger = logging.getLogger(__name__)

if __name__ == "__main__":
    app = AppHeatonCA()
    app.exec()
    app.shutdown()
