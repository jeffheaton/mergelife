/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.mergelife;

import java.awt.image.BufferedImage;
import java.awt.image.WritableRaster;

/**
 * Visualize the merge life grid.
 */
public class VisualizeGridImage {

    /**
     * The image.
     */
    private final BufferedImage image;

    /**
     * The raster.
     */
    private final WritableRaster raster;
    /**
     * The grid.
     */
    private final MergeLifeGrid grid;
    /**
     * The zoom.
     */
    private final int zoom;

    /**
     * The constructor.
     *
     * @param theGrid The universe.
     * @param theZoom     The zoom factor.
     */
    public VisualizeGridImage(final MergeLifeGrid theGrid, final int theZoom) {
        this.grid = theGrid;
        final int width = this.grid.getCols();
        final int height = this.grid.getRows();

        this.image = new BufferedImage(width * theZoom, height * theZoom,
                BufferedImage.TYPE_INT_RGB);
        this.zoom = theZoom;
        this.raster = this.image.getRaster();
    }

    /**
     * Create the image.
     *
     * @param pixels The pixels.
     * @param width  The width.
     * @param height The height.
     * @return The image.
     */
    private BufferedImage createImage(final int[] pixels, final int width,
                              final int height) {
        this.raster.setPixels(0, 0, this.image.getWidth(),
                this.image.getHeight(), pixels);
        return this.image;
    }

    /**
     * @return The universe rendered to an image.
     */
    public BufferedImage visualize(int desiredGrid) {
        final int width = this.grid.getCols();
        final int height = this.grid.getRows();
        final int imageSize = width * height;

        final int[] pixels = new int[imageSize * this.zoom * this.zoom * 3];
        final int rowSize = width * 3 * this.zoom;

        for (int row = 0; row < height; row++) {
            for (int col = 0; col < width; col++) {
                for (int i = 0; i < 3; i++) {
                    for (int y = 0; y < this.zoom; y++) {
                        for (int x = 0; x < this.zoom; x++) {
                            int idx = (row * this.zoom + y) * rowSize
                                    + (col * this.zoom + x) * 3;
                            pixels[idx + i] = this.grid.getGrid(0)[row][col][i];
                        }
                    }
                }
            }
        }

        return createImage(pixels, width * this.zoom, height * this.zoom);
    }
}

