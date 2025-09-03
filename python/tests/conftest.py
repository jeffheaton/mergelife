"""Shared pytest fixtures for MergeLife testing."""

import pytest
import numpy as np
import tempfile
import shutil
from pathlib import Path
from unittest.mock import MagicMock, patch


@pytest.fixture
def temp_dir():
    """Create a temporary directory for tests."""
    temp_dir = tempfile.mkdtemp()
    yield Path(temp_dir)
    shutil.rmtree(temp_dir)


@pytest.fixture
def sample_grid():
    """Create a sample 10x10 grid for testing."""
    return np.random.rand(10, 10) * 7


@pytest.fixture
def sample_rule_code():
    """Provide a sample 16-byte rule code for testing."""
    return "51a24a5a7e5a4a5a7e5a4a5a7e5a4a5a"


@pytest.fixture
def sample_rule_hex():
    """Provide sample hex rule data for testing."""
    return np.array([
        [0x51, 0xa2], [0x4a, 0x5a], [0x7e, 0x5a], [0x4a, 0x5a],
        [0x7e, 0x5a], [0x4a, 0x5a], [0x7e, 0x5a], [0x4a, 0x5a]
    ])


@pytest.fixture
def mock_ml_instance():
    """Create a mock MergeLife instance for testing."""
    return {
        'height': 10,
        'width': 10,
        'grid': np.random.rand(10, 10) * 7,
        'sorted_rule': [(0, 0.5, 0), (8, -0.3, 1), (16, 0.2, 2)],
        'rule': np.array([[0x51, 0xa2], [0x4a, 0x5a]])
    }


@pytest.fixture
def mock_config():
    """Create a mock configuration for testing."""
    return {
        'rows': 100,
        'cols': 100,
        'zoom': 5,
        'renderSteps': 250,
        'populationSize': 1000,
        'tournamentSize': 5,
        'maxGenerations': 100,
        'mutationFactor': 0.1,
        'crossoverFactor': 0.75,
        'evalCycles': 300,
        'scoreThreshold': 5.0,
        'objective': [
            {'stat': 'steps', 'min': 250, 'max': 250, 'weight': 1.0},
            {'stat': 'foreground', 'min': 0.1, 'max': 0.4, 'weight': 1.0}
        ]
    }


@pytest.fixture
def mock_image_array():
    """Create a mock RGB image array for testing."""
    return np.random.randint(0, 256, (50, 50, 3), dtype=np.uint8)


@pytest.fixture
def mock_logger():
    """Create a mock logger for testing."""
    return MagicMock()


@pytest.fixture
def mock_file_path(temp_dir):
    """Create a mock file path in the temp directory."""
    return temp_dir / "test_file.png"


@pytest.fixture(autouse=True)
def reset_random_seed():
    """Reset random seed before each test for reproducibility."""
    np.random.seed(42)


@pytest.fixture
def mock_scipy_stats():
    """Mock scipy.stats functions."""
    with patch('scipy.stats.kurtosis') as mock_kurtosis, \
         patch('scipy.stats.skew') as mock_skew:
        mock_kurtosis.return_value = 2.5
        mock_skew.return_value = 0.8
        yield {
            'kurtosis': mock_kurtosis,
            'skew': mock_skew
        }


@pytest.fixture
def mock_pil_image():
    """Mock PIL Image for testing."""
    with patch('PIL.Image.fromarray') as mock_fromarray, \
         patch('PIL.Image.new') as mock_new:
        mock_img = MagicMock()
        mock_fromarray.return_value = mock_img
        mock_new.return_value = mock_img
        yield {
            'fromarray': mock_fromarray,
            'new': mock_new,
            'image': mock_img
        }


@pytest.fixture
def evolution_config():
    """Configuration for evolution testing."""
    return {
        'population_size': 10,
        'generations': 5,
        'mutation_rate': 0.1,
        'crossover_rate': 0.7,
        'tournament_size': 3
    }


@pytest.fixture
def objective_function_params():
    """Parameters for objective function testing."""
    return {
        'target_steps': 250,
        'foreground_range': (0.1, 0.4),
        'activity_range': (0.05, 0.8),
        'mass_range': (0.05, 0.8)
    }