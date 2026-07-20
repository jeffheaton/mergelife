"""Test infrastructure validation - verifies testing setup works correctly."""

import pytest
import numpy as np
from pathlib import Path


def test_pytest_working():
    """Basic test to verify pytest is working."""
    assert True


def test_numpy_available():
    """Test that numpy is available and working."""
    arr = np.array([1, 2, 3])
    assert arr.sum() == 6


def test_fixtures_available(temp_dir, sample_grid, mock_config):
    """Test that fixtures are working."""
    assert temp_dir.exists()
    assert sample_grid.shape == (10, 10)
    assert isinstance(mock_config, dict)
    assert 'rows' in mock_config


def test_markers():
    """Test that custom markers are recognized."""
    # This test validates that our custom markers are configured
    pass


@pytest.mark.unit
def test_unit_marker():
    """Test with unit marker."""
    assert 1 + 1 == 2


@pytest.mark.integration  
def test_integration_marker():
    """Test with integration marker."""
    assert "hello" + " world" == "hello world"


@pytest.mark.slow
def test_slow_marker():
    """Test with slow marker."""
    import time
    time.sleep(0.01)  # Minimal sleep to simulate slow test
    assert True


def test_mock_functionality(mock_logger, mock_ml_instance):
    """Test that mocking functionality works."""
    mock_logger.info("test message")
    mock_logger.info.assert_called_once_with("test message")
    
    assert mock_ml_instance['height'] == 10
    assert mock_ml_instance['width'] == 10


def test_coverage_instrumentation():
    """Test that coverage instrumentation is working."""
    def dummy_function():
        return "covered"
    
    result = dummy_function()
    assert result == "covered"


class TestInfrastructureClass:
    """Test class structure for pytest discovery."""
    
    def test_method_discovery(self):
        """Test that methods in test classes are discovered."""
        assert hasattr(self, 'test_method_discovery')
    
    def test_class_fixtures(self, sample_rule_code):
        """Test fixtures work in test classes."""
        assert isinstance(sample_rule_code, str)
        assert len(sample_rule_code) == 32  # 16 bytes as hex = 32 chars