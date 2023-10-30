import sys
from PyQt6.QtWidgets import QApplication, QMainWindow, QTabWidget, QVBoxLayout, QWidget, QLabel
import sys
from PyQt6.QtWidgets import QApplication, QMainWindow, QScrollArea, QWidget, QGridLayout, QLabel, QVBoxLayout, QSizePolicy
from PyQt6.QtGui import QPixmap, QImageReader, QImage
from PyQt6.QtCore import Qt
from mergelife import new_ml_instance, update_step, randomize_lattice
import numpy as np


class GalleryTab(QWidget):
    GALLERY_RULES = [
        "e542-5f79-9341-f31e-6c6b-7f08-8773-7068",
        "a07f-c000-0000-0000-0000-0000-ff80-807f",
        "6eb6-ba3d-70b4-ac6f-baae-2604-8529-8998",
        "ea44-55df-9025-bead-5f6e-45ca-6168-275a",
        "7b58-f7b4-c5b4-fd87-22fa-eb10-6de8-107c",
        "8503-5eb6-084c-04df-7657-a5b3-6044-3524",
        "1c48-9004-8831-41be-2804-8f50-9901-db18",
        "df1d-bba1-8e06-aa66-48ff-7414-6a2f-6237",
        "6769-5dd6-7d03-564e-a5ec-cae2-54c4-810c",
        "cb97-6a74-88c0-28aa-1b6a-834b-4fe8-60ac",
        "6007-7d42-05e5-1b9b-2899-e043-1cd4-2f7b",
        "dfda-67af-bc97-7ef6-be98-42d9-9147-97d3",
        "f81b-38d1-7f60-62ad-850b-2085-ddff-8154",
        "548c-aac9-97d2-b8dd-1425-88c1-599d-78e2",
        "8501-677e-655f-236e-53ba-d52d-8cf1-1e46",
        "5688-0f6c-6619-8605-d7e4-4074-de2e-96c8",
        "c168-7b61-b5cc-4e64-8f7a-df90-5362-8750",
        "5eb3-2d3b-df40-ee83-e472-60c3-3342-48be",
        "5a3d-45de-96fd-de64-ecf9-77c1-8461-9c8c",
        "2085-c66a-84d8-fbe8-b3c0-70e4-0e2e-799c",
        "5a55-983c-daad-60f5-2969-3077-90e7-9188",
        "6da1-0852-5e0f-2ad9-c902-f8a0-78fd-4473",
        "a45d-d552-3331-a34f-890a-bb71-64e2-c4f0",
        "d106-f969-cda8-ceb6-9964-977c-cc43-62b1",
        "2152-9b71-abb7-162a-45ff-dd03-fe15-957e",
        "2152-9b71-bbc7-162a-c5ff-ad03-fd65-957e",
        "4d56-d1e3-4acb-60d6-5e2f-5fbf-33ad-e266",
        "7b58-f7b4-a5b4-fd87-22fa-eb12-6de8-107f",
        "bf51-3628-3bcf-1ee1-5b18-7b95-7898-6a9a",
        "ef12-d680-9430-8853-a368-55f9-7451-7c44"
        ]
    def __init__(self, window):
        super().__init__()
        self._window = window

        # Create the scroll area and set its properties
        self.scroll_area = QScrollArea(self)
        self.scroll_area.setWidgetResizable(True)
        self.scroll_area.setHorizontalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAlwaysOff)
        self.scroll_area.setVerticalScrollBarPolicy(Qt.ScrollBarPolicy.ScrollBarAsNeeded)

        # Create the widget that will be contained inside the scroll area
        self.grid_widget = QWidget()
        self.layout = QGridLayout(self.grid_widget)  # Set the QGridLayout to grid_widget
        self.grid_widget.setLayout(self.layout)
        self.scroll_area.setWidget(self.grid_widget)

        # Layout for the GalleryTab
        self.main_layout = QVBoxLayout(self)  # Set QVBoxLayout to GalleryTab
        self.main_layout.addWidget(self.scroll_area)
        self.setLayout(self.main_layout)

        # Continue with your images
        self.add_images(GalleryTab.GALLERY_RULES)

    def rule2img(self, rule, height, width, cell_size):
        ml = new_ml_instance(height,width,rule)
        randomize_lattice(ml)

        for i in range(50):
            update_step(ml)


        grid = ml['lattice'][0]['data']

        render_buffer = np.zeros((height * cell_size, width * cell_size, 3), 
            dtype=np.uint8)

        for x in range(width):
            for y in range(height):
                color = grid[y][x]
                render_buffer[y*cell_size:(y+1)*cell_size, x*cell_size:(x+1)*cell_size] = color 

        # Update QPixmap for the existing QGraphicsPixmapItem
        display_buffer = QImage(render_buffer.data, width * cell_size, 
                    height * cell_size, 3 * width * cell_size, 
                    QImage.Format.Format_RGB888)
        return QPixmap.fromImage(display_buffer)

    def add_images(self, rules):
        for idx, rule in enumerate(rules):
            pixmap = self.rule2img(
                rule,
                64,100,3)
            

            # Create a label to show the image and set the pixmap
            img_label = QLabel()
            img_label.setPixmap(pixmap)
            img_label.setAlignment(Qt.AlignmentFlag.AlignCenter) # pixmap.scaled(128, 1024, Qt.AspectRatioMode.KeepAspectRatio)
            img_label.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Preferred)
                
            # Create a label for the caption
            caption_label = QLabel(rule)
            caption_label.setAlignment(Qt.AlignmentFlag.AlignCenter)
            caption_label.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Preferred)

            # Create QVBoxLayout and add both the image and caption to it
            vbox = QVBoxLayout()
            vbox.addWidget(img_label)
            vbox.addWidget(caption_label)
            vbox.setStretchFactor(img_label, 0)
            vbox.setStretchFactor(caption_label, 0)
            vbox.setContentsMargins(0, 0, 0, 0)  # set margins to 0
            vbox.setSpacing(0)
                
            # Create a QWidget to hold the QVBoxLayout and apply a border to it
            container = QWidget(self)
            container.setLayout(vbox)
            container.setStyleSheet("border: 1px solid black;")  # set border to container
            container.setSizePolicy(QSizePolicy.Policy.Preferred, QSizePolicy.Policy.Fixed)  # <-- Important line

            # Connect the mouse press event of the container to the image_clicked function
            container.mousePressEvent = lambda event, cap=rule: self.image_clicked(cap)

            # Add the container QWidget to the main grid layout
            row, col = divmod(idx, 3)
            self.layout.addWidget(container, row, col)

    def image_clicked(self, caption):
        print(caption)
        self._window.display_rule(caption)

    def on_close(self):
        pass

    def on_resize(self):
        pass