/*
 * MergeLife, Copyrighr 2018 by Jeff Heaton
 * http://www.heatonresearch.com/mergelife/
 * MIT License
 */
package org.heatonresearch.util;

import org.heatonresearch.mergelife.MergeLife;
import org.junit.Test;

public class TestMergeLife {
    @Test
    public void testRender() {
        String[] args = { "-rows", "50", "-cols", "50", "-renderSteps", "250", "-zoom", "1", "render", "E542-5F79-9341-F31E-6C6B-7F08-8773-7068"};
        MergeLife.main(args);
    }
}
